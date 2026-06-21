import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, Subject, catchError, finalize, map, of, tap } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { resolveHttpErrorMessage } from '../../../core/http/http-error.utils';
import { NotificationService } from '../../../core/notification/notification.service';
import {
  JiraAuthUrlResponse,
  JiraConnectionStatus,
  JiraConnectionStatusResponse
} from '../models/jira-connection.model';
import {
  buildOAuthPopupFeatures,
  isJiraOAuthCompleteMessage
} from '../models/jira-oauth.model';

@Injectable({ providedIn: 'root' })
export class JiraConnectionService {
  private readonly http = inject(HttpClient);
  private readonly notificationService = inject(NotificationService);
  private readonly apiBase = `${environment.apiUrl}/jira`;

  private readonly statusSignal = signal<JiraConnectionStatus | null>(null);
  private readonly statusKnownSignal = signal(false);
  private readonly ticketsRefreshSubject = new Subject<void>();
  private oauthListenerRegistered = false;
  private activePopupMonitor: number | null = null;

  readonly status = this.statusSignal.asReadonly();
  readonly statusKnown = this.statusKnownSignal.asReadonly();
  readonly isConnected = computed(() => this.statusSignal()?.connected ?? false);
  readonly isLoading = signal(false);
  readonly isConnecting = signal(false);
  readonly ticketsRefresh$ = this.ticketsRefreshSubject.asObservable();

  loadStatus(): Observable<JiraConnectionStatus> {
    this.isLoading.set(true);

    return this.http.get<JiraConnectionStatusResponse>(`${this.apiBase}/connection`).pipe(
      map((response) => this.toStatus(response)),
      tap((status) => this.statusSignal.set(status)),
      catchError(() => {
        const disconnected: JiraConnectionStatus = {
          connected: false,
          workspaceName: null,
          workspaceUrl: null
        };
        this.statusSignal.set(disconnected);
        return of(disconnected);
      }),
      finalize(() => {
        this.isLoading.set(false);
        this.statusKnownSignal.set(true);
      })
    );
  }

  checkConnectionStatus(): Observable<JiraConnectionStatus> {
    return this.loadStatus();
  }

  reset(): void {
    this.statusSignal.set(null);
    this.statusKnownSignal.set(false);
    this.isConnecting.set(false);
    this.clearPopupMonitor();
  }

  getAuthUrl(): Observable<JiraAuthUrlResponse> {
    return this.http.get<JiraAuthUrlResponse>(`${this.apiBase}/auth-url`);
  }

  connect(): void {
    if (this.isConnecting()) {
      return;
    }

    this.registerOAuthListener();
    this.isConnecting.set(true);

    this.getAuthUrl().subscribe({
      next: (response) => {
        const popup = window.open(response.url, 'jira-oauth', buildOAuthPopupFeatures());
        if (!popup) {
          this.isConnecting.set(false);
          this.notificationService.showError(
            'Popup blocked. Allow popups for this site and try again.'
          );
          return;
        }

        popup.focus();
        this.monitorOAuthPopup(popup);
      },
      error: (error: unknown) => {
        this.isConnecting.set(false);
        const message =
          error instanceof HttpErrorResponse
            ? resolveHttpErrorMessage(error)
            : 'Unable to start Jira authorization.';
        this.notificationService.showError(message);
      }
    });
  }

  completeOAuthPopup(connected: boolean): void {
    this.isConnecting.set(false);
    this.notifyOpener(connected);
    window.close();
  }

  notifyTicketCreated(): void {
    this.ticketsRefreshSubject.next();
  }

  private registerOAuthListener(): void {
    if (this.oauthListenerRegistered) {
      return;
    }

    window.addEventListener('message', (event: MessageEvent) => {
      if (event.origin !== window.location.origin) {
        return;
      }

      if (!isJiraOAuthCompleteMessage(event.data)) {
        return;
      }

      this.clearPopupMonitor();
      this.isConnecting.set(false);

      this.loadStatus().subscribe({
        next: (status) => {
          if (status.connected) {
            this.notificationService.showSuccess('Jira workspace connected successfully.');
          } else {
            this.notificationService.showError('Jira connection could not be verified.');
          }
        },
        error: () => {
          this.notificationService.showError('Failed to verify Jira connection.');
        }
      });
    });

    this.oauthListenerRegistered = true;
  }

  private monitorOAuthPopup(popup: Window): void {
    this.clearPopupMonitor();

    this.activePopupMonitor = window.setInterval(() => {
      if (!popup.closed) {
        return;
      }

      this.clearPopupMonitor();
      if (this.isConnecting()) {
        this.isConnecting.set(false);
        this.loadStatus().subscribe();
      }
    }, 500);
  }

  private notifyOpener(connected: boolean): void {
    if (!window.opener || window.opener.closed) {
      return;
    }

    window.opener.postMessage(
      { type: 'jira-oauth-complete', connected },
      window.location.origin
    );
  }

  private clearPopupMonitor(): void {
    if (this.activePopupMonitor !== null) {
      window.clearInterval(this.activePopupMonitor);
      this.activePopupMonitor = null;
    }
  }

  private toStatus(response: JiraConnectionStatusResponse): JiraConnectionStatus {
    const raw = response as JiraConnectionStatusResponse & {
      Connected?: boolean;
      WorkspaceName?: string | null;
      WorkspaceUrl?: string | null;
    };

    return {
      connected: raw.connected ?? raw.Connected ?? false,
      workspaceName: raw.workspaceName ?? raw.WorkspaceName ?? null,
      workspaceUrl: raw.workspaceUrl ?? raw.WorkspaceUrl ?? null
    };
  }
}
