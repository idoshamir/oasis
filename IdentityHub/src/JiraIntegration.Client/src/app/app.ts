import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { ServerUnavailableBannerComponent } from './core/http/server-unavailable-banner.component';
import { NotificationToastComponent } from './core/notification/notification-toast.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NotificationToastComponent, ServerUnavailableBannerComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {}
