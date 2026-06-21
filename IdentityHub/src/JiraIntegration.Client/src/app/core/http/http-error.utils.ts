import { HttpErrorResponse } from '@angular/common/http';

import { ErrorResponse } from '../auth/auth.models';

export const SERVER_UNREACHABLE_MESSAGE =
  'Unable to reach the server. Check that the API is running.';

const defaultMessages: Readonly<Record<number, string>> = {
  0: SERVER_UNREACHABLE_MESSAGE,
  400: 'The request could not be processed. Please check your input.',
  403: 'You do not have permission to perform this action.',
  502: 'The server is temporarily unavailable. Please try again later.',
  503: 'The service is temporarily unavailable. Please try again later.',
  504: 'The server is temporarily unavailable. Please try again later.',
  500: 'An unexpected server error occurred. Please try again later.'
};

export function extractHttpErrorMessage(error: HttpErrorResponse): string | null {
  const body = error.error;

  if (typeof body === 'string' && body.trim()) {
    return body.trim();
  }

  if (!body || typeof body !== 'object') {
    return null;
  }

  const errorBody = body as ErrorResponse & Record<string, unknown>;
  const candidates = [errorBody.message, errorBody['Message'], errorBody['detail'], errorBody['title']];

  for (const candidate of candidates) {
    if (typeof candidate === 'string' && candidate.trim()) {
      return candidate.trim();
    }
  }

  return null;
}

export function resolveHttpErrorMessage(error: HttpErrorResponse): string {
  return extractHttpErrorMessage(error) ?? defaultMessages[error.status] ?? 'An unexpected error occurred.';
}

export function isApiRequest(url: string, apiUrl: string): boolean {
  return url.startsWith(apiUrl);
}

export function isServerUnreachableError(error: HttpErrorResponse): boolean {
  return error.status === 0 || error.status === 502 || error.status === 503 || error.status === 504;
}
