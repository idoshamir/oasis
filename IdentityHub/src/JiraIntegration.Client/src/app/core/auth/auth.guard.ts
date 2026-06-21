import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return authService.tryRestoreSession().pipe(
    map((restored) => (restored ? true : router.createUrlTree(['/login'])))
  );
};

export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return router.createUrlTree(['/dashboard']);
  }

  return authService.tryRestoreSession().pipe(
    map((restored) => (restored ? router.createUrlTree(['/dashboard']) : true))
  );
};

export const fallbackRedirectGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return router.createUrlTree(['/dashboard']);
  }

  return authService.tryRestoreSession().pipe(
    map((restored) =>
      restored ? router.createUrlTree(['/dashboard']) : router.createUrlTree(['/login'])
    )
  );
};
