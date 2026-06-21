export interface ApiKeyListItem {
  id: string;
  name: string;
  projectKey: string;
  maskedKey: string;
  createdAt: string;
}

export interface ApiKeyListResponse {
  keys: ApiKeyListItem[];
}

export interface GenerateApiKeyRequest {
  name: string;
  projectKey: string;
}

export interface GenerateApiKeyResponse {
  id: string;
  name: string;
  projectKey: string;
  plaintextKey: string;
  createdAt: string;
}
