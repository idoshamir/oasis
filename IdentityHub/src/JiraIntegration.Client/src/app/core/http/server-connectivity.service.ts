import { Injectable, signal } from '@angular/core';

import { SERVER_UNREACHABLE_MESSAGE } from './http-error.utils';

@Injectable({ providedIn: 'root' })
export class ServerConnectivityService {
  readonly isServerUnavailable = signal(false);
  readonly message = signal(SERVER_UNREACHABLE_MESSAGE);

  markUnavailable(customMessage?: string): boolean {
    const wasAvailable = !this.isServerUnavailable();

    if (customMessage?.trim()) {
      this.message.set(customMessage.trim());
    } else if (wasAvailable) {
      this.message.set(SERVER_UNREACHABLE_MESSAGE);
    }

    this.isServerUnavailable.set(true);
    return wasAvailable;
  }

  markAvailable(): void {
    this.isServerUnavailable.set(false);
    this.message.set(SERVER_UNREACHABLE_MESSAGE);
  }
}
