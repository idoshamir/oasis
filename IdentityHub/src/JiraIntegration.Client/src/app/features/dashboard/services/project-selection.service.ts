import { Injectable, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';

import { JiraProjectOption, TicketService } from './ticket.service';

@Injectable({ providedIn: 'root' })
export class ProjectSelectionService {
  private readonly ticketService = inject(TicketService);

  private readonly projectsSignal = signal<readonly JiraProjectOption[]>([]);
  private readonly selectedProjectKeySignal = signal<string | null>(null);
  private readonly isLoadingSignal = signal(false);
  private readonly loadFailedSignal = signal(false);

  readonly projects = this.projectsSignal.asReadonly();
  readonly selectedProjectKey = this.selectedProjectKeySignal.asReadonly();
  readonly isLoading = this.isLoadingSignal.asReadonly();
  readonly loadFailed = this.loadFailedSignal.asReadonly();
  readonly hasProjects = computed(() => this.projectsSignal().length > 0);
  readonly hasSelection = computed(() => this.selectedProjectKeySignal() !== null);
  readonly isSelectionReady = computed(
    () => !this.isLoadingSignal() && this.selectedProjectKeySignal() !== null
  );

  readonly selectedProject = computed(() => {
    const key = this.selectedProjectKeySignal();
    if (!key) {
      return null;
    }

    return this.projectsSignal().find((project) => project.key === key) ?? null;
  });

  loadProjects(): void {
    this.isLoadingSignal.set(true);
    this.loadFailedSignal.set(false);

    this.ticketService
      .getProjects()
      .pipe(finalize(() => this.isLoadingSignal.set(false)))
      .subscribe({
        next: (response) => {
          this.projectsSignal.set(response.projects);
          this.selectedProjectKeySignal.set(response.projects[0]?.key ?? null);
        },
        error: () => {
          this.projectsSignal.set([]);
          this.loadFailedSignal.set(true);
        }
      });
  }

  selectProject(projectKey: string): void {
    const match = this.projectsSignal().some((project) => project.key === projectKey);
    if (match) {
      this.selectedProjectKeySignal.set(projectKey);
    }
  }

  clearSelection(): void {
    this.selectedProjectKeySignal.set(null);
    this.projectsSignal.set([]);
    this.loadFailedSignal.set(false);
  }
}
