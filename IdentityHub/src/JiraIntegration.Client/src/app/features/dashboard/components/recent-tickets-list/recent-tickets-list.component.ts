import { DatePipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, interval } from 'rxjs';

import { RecentTicket } from '../../models/recent-ticket.model';
import { JiraConnectionService } from '../../services/jira-connection.service';
import { ProjectSelectionService } from '../../services/project-selection.service';
import { TicketService } from '../../services/ticket.service';

@Component({
  selector: 'app-recent-tickets-list',
  imports: [DatePipe],
  templateUrl: './recent-tickets-list.component.html',
  styleUrl: './recent-tickets-list.component.scss'
})
export class RecentTicketsListComponent {
  private static readonly refreshIntervalMs = 10_000;

  private readonly ticketService = inject(TicketService);
  private readonly jiraConnectionService = inject(JiraConnectionService);
  protected readonly projectSelection = inject(ProjectSelectionService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly tickets = signal<readonly RecentTicket[]>([]);
  protected readonly isLoading = signal(false);

  private loadGeneration = 0;

  constructor() {
    effect(() => {
      const projectKey = this.projectSelection.selectedProjectKey();
      const isLoadingProjects = this.projectSelection.isLoading();

      if (!projectKey || isLoadingProjects) {
        if (!projectKey) {
          this.tickets.set([]);
        }

        return;
      }

      this.loadTickets(projectKey);
    });

    this.jiraConnectionService.ticketsRefresh$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.refreshTickets(false);
      });

    interval(RecentTicketsListComponent.refreshIntervalMs)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.refreshTickets(false);
      });
  }

  private refreshTickets(showLoading: boolean): void {
    const projectKey = this.projectSelection.selectedProjectKey();
    if (projectKey && !this.projectSelection.isLoading()) {
      this.loadTickets(projectKey, showLoading);
    }
  }

  private loadTickets(projectKey: string, showLoading = true): void {
    const generation = ++this.loadGeneration;

    if (showLoading) {
      this.isLoading.set(true);
    }

    this.ticketService
      .getRecentTickets(projectKey)
      .pipe(
        finalize(() => {
          if (generation === this.loadGeneration && showLoading) {
            this.isLoading.set(false);
          }
        })
      )
      .subscribe({
        next: (response) => {
          if (generation !== this.loadGeneration) {
            return;
          }

          const tickets = response.tickets.map((ticket) => ({
            issueKey: ticket.issueKey,
            title: ticket.title,
            createdAt: new Date(ticket.createdAt),
            externalUrl: ticket.externalUrl
          }));

          this.tickets.set(tickets);
        },
        error: () => {
          if (generation !== this.loadGeneration) {
            return;
          }

          if (showLoading) {
            this.tickets.set([]);
          }
        }
      });
  }
}
