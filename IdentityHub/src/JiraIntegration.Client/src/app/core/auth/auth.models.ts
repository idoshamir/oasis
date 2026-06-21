export const TOKEN_STORAGE_KEY = 'auth_token';

export interface AuthUser {
  username: string;
}

export interface LoginCredentials {
  username: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
}

export interface ErrorResponse {
  message: string;
  code?: string | null;
}
