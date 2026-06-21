import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';

import { NotificationService } from '../../core/notification/notification.service';
import { JiraConnectionService } from './services/jira-connection.service';

@Component({
  selector: 'app-jira-connected',
  template: `
    <main class="callback-page">
      <p>Finalizing Jira connection...</p>
    </main>
  `,
  styles: `
    .callback-page {
      min-height: 100vh;
      display: grid;
      place-items: center;
      color: var(--color-text-muted);
    }
  `
})
export class JiraConnectedComponent implements OnInit {
  private readonly jiraConnectionService = inject(JiraConnectionService);
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    const isPopup = Boolean(window.opener && !window.opener.closed);
    const params = new URLSearchParams(window.location.search);
    const jiraConnected = params.get('jira_connected');

    if (jiraConnected === 'false') {
      this.handleCallbackResult(isPopup, false);
      return;
    }

    this.jiraConnectionService.checkConnectionStatus().subscribe({
      next: (status) => {
        this.handleCallbackResult(isPopup, status.connected);
      },
      error: () => {
        this.handleCallbackResult(isPopup, false);
      }
    });
  }

  private handleCallbackResult(isPopup: boolean, connected: boolean): void {
    if (isPopup) {
      this.jiraConnectionService.completeOAuthPopup(connected);
      return;
    }

    if (connected) {
      this.notificationService.showSuccess('Jira workspace connected successfully.');
    } else {
      this.notificationService.showError('Jira connection could not be verified.');
    }

    void this.router.navigate(['/dashboard']);
  }
}
