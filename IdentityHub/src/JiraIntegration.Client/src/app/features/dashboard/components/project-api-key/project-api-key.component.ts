import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { NotificationService } from '../../../../core/notification/notification.service';
import { ApiKeyListItem } from '../../models/api-key.model';
import { ApiKeyService } from '../../services/api-key.service';
import { ProjectSelectionService } from '../../services/project-selection.service';

@Component({
  selector: 'app-project-api-key',
  templateUrl: './project-api-key.component.html',
  styleUrl: './project-api-key.component.scss'
})
export class ProjectApiKeyComponent {
  private readonly apiKeyService = inject(ApiKeyService);
  private readonly projectSelection = inject(ProjectSelectionService);
  private readonly notificationService = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly existingKey = signal<ApiKeyListItem | null>(null);
  protected readonly plaintextKey = signal<string | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly isSubmitting = signal(false);

  constructor() {
    effect(() => {
      const projectKey = this.projectSelection.selectedProjectKey();
      this.plaintextKey.set(null);

      if (!projectKey) {
        this.existingKey.set(null);
        return;
      }

      this.loadKeyForProject(projectKey);
    });
  }

  protected get hasKey(): boolean {
    return this.plaintextKey() !== null || this.existingKey() !== null;
  }

  protected get canCopy(): boolean {
    return this.plaintextKey() !== null;
  }

  protected generateKey(): void {
    const projectKey = this.projectSelection.selectedProjectKey();
    if (!projectKey || this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);
    this.apiKeyService
      .generateKey({ name: `${projectKey} API Key`, projectKey })
      .pipe(
        finalize(() => this.isSubmitting.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (response) => {
          this.plaintextKey.set(response.plaintextKey);
          this.existingKey.set({
            id: response.id,
            name: response.name,
            projectKey: response.projectKey,
            maskedKey: `${response.plaintextKey.slice(0, 8)}...`,
            createdAt: response.createdAt
          });
          this.notificationService.showSuccess('API key generated.');
        },
        error: () => {
          this.notificationService.showError('Unable to generate API key. Please try again.');
        }
      });
  }

  protected regenerateKey(): void {
    const projectKey = this.projectSelection.selectedProjectKey();
    if (!projectKey || this.isSubmitting()) {
      return;
    }

    const confirmed = window.confirm(
      'Regenerating will revoke the current key and create a new one. Continue?'
    );

    if (!confirmed) {
      return;
    }

    this.isSubmitting.set(true);
    this.apiKeyService
      .regenerateKey(projectKey)
      .pipe(
        finalize(() => this.isSubmitting.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (response) => {
          this.plaintextKey.set(response.plaintextKey);
          this.existingKey.set({
            id: response.id,
            name: response.name,
            projectKey: response.projectKey,
            maskedKey: `${response.plaintextKey.slice(0, 8)}...`,
            createdAt: response.createdAt
          });
          this.notificationService.showSuccess('API key regenerated.');
        },
        error: () => {
          this.notificationService.showError('Unable to regenerate API key. Please try again.');
        }
      });
  }

  protected async copyKey(): Promise<void> {
    const key = this.plaintextKey();
    if (!key) {
      return;
    }

    try {
      await navigator.clipboard.writeText(key);
      this.notificationService.showSuccess('Key copied to clipboard!');
    } catch {
      this.notificationService.showError('Unable to copy key to clipboard.');
    }
  }

  private loadKeyForProject(projectKey: string): void {
    this.isLoading.set(true);

    this.apiKeyService
      .listKeys()
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (response) => {
          const match = response.keys.find((key) => key.projectKey === projectKey) ?? null;
          this.existingKey.set(match);
        },
        error: () => {
          this.existingKey.set(null);
        }
      });
  }
}
