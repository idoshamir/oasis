export interface JiraConnectionStatus {
  connected: boolean;
  workspaceName: string | null;
  workspaceUrl: string | null;
}

export interface JiraConnectionStatusResponse {
  connected: boolean;
  workspaceName: string | null;
  workspaceUrl: string | null;
}

export interface JiraAuthUrlResponse {
  url: string;
}
