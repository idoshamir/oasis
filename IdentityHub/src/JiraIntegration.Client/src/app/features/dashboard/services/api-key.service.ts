import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  ApiKeyListResponse,
  GenerateApiKeyRequest,
  GenerateApiKeyResponse
} from '../models/api-key.model';

@Injectable({ providedIn: 'root' })
export class ApiKeyService {
  private readonly http = inject(HttpClient);
  private readonly apiBase = `${environment.apiUrl}/ui/api-keys`;

  listKeys(): Observable<ApiKeyListResponse> {
    return this.http.get<ApiKeyListResponse>(this.apiBase);
  }

  generateKey(request: GenerateApiKeyRequest): Observable<GenerateApiKeyResponse> {
    return this.http.post<GenerateApiKeyResponse>(this.apiBase, {
      name: request.name,
      projectKey: request.projectKey
    });
  }

  revokeKey(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiBase}/${id}`);
  }

  regenerateKey(projectKey: string): Observable<GenerateApiKeyResponse> {
    return this.http.post<GenerateApiKeyResponse>(`${this.apiBase}/regenerate`, { projectKey });
  }
}
