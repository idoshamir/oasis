import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';

import { NotificationService } from '../../../../core/notification/notification.service';
import { JiraConnectionService } from '../../services/jira-connection.service';
import { ProjectSelectionService } from '../../services/project-selection.service';
import { TicketService } from '../../services/ticket.service';

interface TicketFormControls {
  title: FormControl<string>;
  description: FormControl<string>;
}

type SubmissionState = 'idle' | 'submitting' | 'success' | 'error';

@Component({
  selector: 'app-ticket-creation-form',
  imports: [ReactiveFormsModule],
  templateUrl: './ticket-creation-form.component.html',
  styleUrl: './ticket-creation-form.component.scss'
})
export class TicketCreationFormComponent {
  private readonly ticketService = inject(TicketService);
  private readonly jiraConnectionService = inject(JiraConnectionService);
  private readonly notificationService = inject(NotificationService);
  protected readonly projectSelection = inject(ProjectSelectionService);

  protected readonly submissionState = signal<SubmissionState>('idle');

  protected readonly isSubmitting = computed(() => this.submissionState() === 'submitting');
  protected readonly isSuccess = computed(() => this.submissionState() === 'success');
  protected readonly canSubmit = computed(() => !this.isSubmitting() && this.projectSelection.hasSelection());

  protected readonly form = new FormGroup<TicketFormControls>({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required]
    })
  });

  protected submit(): void {
    if (this.isSubmitting() || !this.canSubmit()) {
      return;
    }

    const projectKey = this.projectSelection.selectedProjectKey();
    if (!projectKey) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submissionState.set('submitting');

    const { title, description } = this.form.getRawValue();

    this.ticketService
      .createTicket({ projectKey, title, description })
      .pipe(
        finalize(() => {
          if (this.submissionState() === 'submitting') {
            this.submissionState.set('idle');
          }
        })
      )
      .subscribe({
        next: (response) => {
          this.submissionState.set('success');
          this.form.controls.title.reset();
          this.form.controls.description.reset();
          this.notificationService.showSuccess(`Ticket ${response.issueKey} created successfully.`);
          this.jiraConnectionService.notifyTicketCreated();

          window.setTimeout(() => {
            if (this.submissionState() === 'success') {
              this.submissionState.set('idle');
            }
          }, 3000);
        },
        error: () => {
          this.submissionState.set('error');

          window.setTimeout(() => {
            if (this.submissionState() === 'error') {
              this.submissionState.set('idle');
            }
          }, 3000);
        }
      });
  }
}
