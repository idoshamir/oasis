import { Routes } from '@angular/router';

import {
  authGuard,
  fallbackRedirectGuard,
  guestGuard
} from './core/auth/auth.guard';
import { FallbackComponent } from './core/routing/fallback.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { JiraConnectedComponent } from './features/dashboard/jira-connected.component';
import { LoginComponent } from './features/login/login.component';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  },
  {
    path: 'login',
    component: LoginComponent,
    canActivate: [guestGuard]
  },
  {
    path: 'dashboard',
    component: DashboardComponent,
    canActivate: [authGuard]
  },
  {
    path: 'jira-connected',
    component: JiraConnectedComponent,
    canActivate: [authGuard]
  },
  {
    path: '**',
    component: FallbackComponent,
    canActivate: [fallbackRedirectGuard]
  }
];
