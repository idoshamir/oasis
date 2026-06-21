import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, tap, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { NotificationService } from '../notification/notification.service';
import {
  isApiRequest,
  isServerUnreachableError,
  resolveHttpErrorMessage
} from './http-error.utils';
import { ServerConnectivityService } from './server-connectivity.service';

const handledErrorStatuses = new Set([400, 403, 500]);

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const notificationService = inject(NotificationService);
  const router = inject(Router);
  const serverConnectivity = inject(ServerConnectivityService);
  const isApi = isApiRequest(request.url, environment.apiUrl);

  return next(request).pipe(
    tap({
      next: () => {
        if (isApi) {
          serverConnectivity.markAvailable();
        }
      }
    }),
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || !isApi) {
        return throwError(() => error);
      }

      const isAuthLoginRequest = request.url.includes('/auth/login');
      const isAuthLogoutRequest = request.url.includes('/auth/logout');

      if (error.status === 401 && !isAuthLoginRequest && !isAuthLogoutRequest) {
        authService.logout().subscribe(() => {
          void router.navigate(['/login']);
        });
        return throwError(() => error);
      }

      if (isServerUnreachableError(error)) {
        const message = resolveHttpErrorMessage(error);
        const isFirstFailure = serverConnectivity.markUnavailable(message);

        if (isFirstFailure && !isAuthLoginRequest) {
          notificationService.showError(message);
        }

        return throwError(() => error);
      }

      if (!isAuthLoginRequest && handledErrorStatuses.has(error.status)) {
        notificationService.showError(resolveHttpErrorMessage(error));
      }

      return throwError(() => error);
    })
  );
};
