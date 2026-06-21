import { Component, inject } from '@angular/core';

import { ServerConnectivityService } from './server-connectivity.service';

@Component({
  selector: 'app-server-unavailable-banner',
  templateUrl: './server-unavailable-banner.component.html',
  styleUrl: './server-unavailable-banner.component.scss'
})
export class ServerUnavailableBannerComponent {
  protected readonly serverConnectivity = inject(ServerConnectivityService);
}
