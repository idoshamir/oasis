import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, tap, throwError } from 'rxjs';

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
const authRetryHeader = 'X-Auth-Retry';

const isAuthEndpoint = (url: string): boolean =>
  url.includes('/auth/login') || url.includes('/auth/refresh') || url.includes('/auth/logout');

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const notificationService = inject(NotificationService);
  const router = inject(Router);
  const serverConnectivity = inject(ServerConnectivityService);
  const isApi = isApiRequest(request.url, environment.apiUrl);

  const send = (req = request) => next(req);

  return send().pipe(
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

      if (error.status === 401 && !isAuthEndpoint(request.url)) {
        if (request.headers.has(authRetryHeader)) {
          authService.logout().subscribe(() => {
            void router.navigate(['/login']);
          });
          return throwError(() => error);
        }

        return authService.refreshSession().pipe(
          switchMap(() => {
            const token = authService.getToken();
            const retryRequest = request.clone({
              setHeaders: {
                Authorization: token ? `Bearer ${token}` : '',
                [authRetryHeader]: 'true'
              },
              withCredentials: true
            });
            return send(retryRequest);
          }),
          catchError(() => {
            authService.logout().subscribe(() => {
              void router.navigate(['/login']);
            });
            return throwError(() => error);
          })
        );
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
