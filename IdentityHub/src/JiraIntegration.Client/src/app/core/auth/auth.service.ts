import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  BehaviorSubject,
  Observable,
  catchError,
  finalize,
  map,
  of,
  shareReplay,
  tap,
  throwError
} from 'rxjs';

import { environment } from '../../../environments/environment';
import { extractHttpErrorMessage, SERVER_UNREACHABLE_MESSAGE } from '../http/http-error.utils';
import {
  AuthResponse,
  AuthUser,
  LoginCredentials,
  TOKEN_STORAGE_KEY
} from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiBase = `${environment.apiUrl}/auth`;
  private readonly httpOptions = { withCredentials: true };

  private readonly currentUserSubject = new BehaviorSubject<AuthUser | null>(this.restoreUser());
  private refreshInFlight: Observable<AuthResponse> | null = null;

  readonly currentUser$ = this.currentUserSubject.asObservable();
  readonly isAuthenticated$ = this.currentUser$.pipe(map((user) => user !== null));

  login(credentials: LoginCredentials): Observable<void> {
    const username = credentials.username.trim();
    const password = credentials.password;

    if (!username || !password) {
      return throwError(() => new Error('Username and password are required.'));
    }

    return this.http
      .post<AuthResponse>(`${this.apiBase}/login`, { username, password }, this.httpOptions)
      .pipe(
        tap((response) => this.persistSession(response)),
        map(() => undefined),
        catchError((error: unknown) => throwError(() => this.mapAuthError(error)))
      );
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.apiBase}/logout`, {}, this.httpOptions).pipe(
      tap(() => this.clearSession()),
      map(() => undefined),
      catchError(() => {
        this.clearSession();
        return of(undefined);
      })
    );
  }

  refreshSession(): Observable<AuthResponse> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.http
        .post<AuthResponse>(`${this.apiBase}/refresh`, {}, this.httpOptions)
        .pipe(
          tap((response) => this.persistSession(response)),
          finalize(() => {
            this.refreshInFlight = null;
          }),
          shareReplay(1)
        );
    }

    return this.refreshInFlight;
  }

  tryRestoreSession(): Observable<boolean> {
    if (this.isAuthenticated()) {
      return of(true);
    }

    return this.refreshSession().pipe(
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      })
    );
  }

  getToken(): string | null {
    return sessionStorage.getItem(TOKEN_STORAGE_KEY);
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) {
      return false;
    }

    if (this.isTokenExpired(token)) {
      return false;
    }

    return true;
  }

  private persistSession(response: AuthResponse): void {
    sessionStorage.setItem(TOKEN_STORAGE_KEY, response.token);

    const username = this.decodeUsername(response.token);
    this.currentUserSubject.next(username ? { username } : null);
  }

  private clearSession(): void {
    sessionStorage.removeItem(TOKEN_STORAGE_KEY);
    this.currentUserSubject.next(null);
  }

  private restoreUser(): AuthUser | null {
    const token = sessionStorage.getItem(TOKEN_STORAGE_KEY);
    if (!token || this.isTokenExpired(token)) {
      return null;
    }

    const username = this.decodeUsername(token);
    return username ? { username } : null;
  }

  private decodeUsername(token: string): string | null {
    try {
      const payloadSegment = token.split('.')[1];
      if (!payloadSegment) {
        return null;
      }

      const payloadJson = atob(payloadSegment.replace(/-/g, '+').replace(/_/g, '/'));
      const payload = JSON.parse(payloadJson) as { unique_name?: unknown };
      return typeof payload.unique_name === 'string' ? payload.unique_name : null;
    } catch {
      return null;
    }
  }

  private isTokenExpired(token: string): boolean {
    try {
      const payloadSegment = token.split('.')[1];
      if (!payloadSegment) {
        return true;
      }

      const payloadJson = atob(payloadSegment.replace(/-/g, '+').replace(/_/g, '/'));
      const payload = JSON.parse(payloadJson) as { exp?: unknown };
      if (typeof payload.exp !== 'number') {
        return false;
      }

      return payload.exp * 1000 <= Date.now();
    } catch {
      return true;
    }
  }

  private mapAuthError(error: unknown): Error {
    if (error instanceof HttpErrorResponse) {
      const message = extractHttpErrorMessage(error);
      if (message) {
        return new Error(message);
      }

      if (error.status === 0) {
        return new Error(SERVER_UNREACHABLE_MESSAGE);
      }

      if (error.status === 401) {
        return new Error('Invalid username or password.');
      }
    }

    return new Error('Unable to sign in. Please try again.');
  }
}
