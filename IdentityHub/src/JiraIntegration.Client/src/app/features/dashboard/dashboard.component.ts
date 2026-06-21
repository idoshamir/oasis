import { AsyncPipe } from '@angular/common';
import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { JiraConnectionService } from './services/jira-connection.service';
import { ProjectSelectionService } from './services/project-selection.service';
import { JiraConnectionStateComponent } from './components/jira-connection-state/jira-connection-state.component';
import { ProjectSelectionComponent } from './components/project-selection/project-selection.component';
import { RecentTicketsListComponent } from './components/recent-tickets-list/recent-tickets-list.component';
import { TicketCreationFormComponent } from './components/ticket-creation-form/ticket-creation-form.component';

@Component({
  selector: 'app-dashboard',
  imports: [
    AsyncPipe,
    JiraConnectionStateComponent,
    ProjectSelectionComponent,
    TicketCreationFormComponent,
    RecentTicketsListComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly jiraConnectionService = inject(JiraConnectionService);
  protected readonly projectSelection = inject(ProjectSelectionService);
  protected readonly currentUser$ = this.authService.currentUser$;

  ngOnInit(): void {
    if (!this.jiraConnectionService.statusKnown()) {
      this.jiraConnectionService.checkConnectionStatus().subscribe();
    }
  }

  protected logout(): void {
    this.authService
      .logout()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.jiraConnectionService.reset();
        this.projectSelection.clearSelection();
        void this.router.navigate(['/login']);
      });
  }
}
