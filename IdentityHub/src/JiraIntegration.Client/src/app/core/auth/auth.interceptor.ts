import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { environment } from '../../../environments/environment';
import { isApiRequest } from '../http/http-error.utils';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request.url, environment.apiUrl)) {
    return next(request);
  }

  const withCredentials = request.clone({ withCredentials: true });
  if (request.url.includes('/auth/login')) {
    return next(withCredentials);
  }

  const token = inject(AuthService).getToken();
  if (!token) {
    return next(withCredentials);
  }

  return next(
    withCredentials.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    })
  );
};
