import { Component, inject } from '@angular/core';

import { NotificationService } from './notification.service';

@Component({
  selector: 'app-notification-toast',
  templateUrl: './notification-toast.component.html',
  styleUrl: './notification-toast.component.scss'
})
export class NotificationToastComponent {
  protected readonly notificationService = inject(NotificationService);
}
