import { Injectable, signal } from '@angular/core';

export type NotificationType = 'error' | 'success' | 'info';

export interface ToastNotification {
  id: number;
  message: string;
  type: NotificationType;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private nextId = 0;

  readonly notifications = signal<readonly ToastNotification[]>([]);

  showError(message: string): void {
    this.show('error', message);
  }

  showSuccess(message: string): void {
    this.show('success', message);
  }

  dismiss(id: number): void {
    this.notifications.update((notifications) => notifications.filter((item) => item.id !== id));
  }

  private show(type: NotificationType, message: string): void {
    const trimmedMessage = message.trim();
    if (!trimmedMessage) {
      return;
    }

    const id = ++this.nextId;
    this.notifications.update((notifications) => [...notifications, { id, message: trimmedMessage, type }]);

    window.setTimeout(() => {
      this.dismiss(id);
    }, 5000);
  }
}
