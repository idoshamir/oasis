export const JIRA_OAUTH_MESSAGE_TYPE = 'jira-oauth-complete';

export interface JiraOAuthCompleteMessage {
  type: typeof JIRA_OAUTH_MESSAGE_TYPE;
  connected: boolean;
}

export function isJiraOAuthCompleteMessage(data: unknown): data is JiraOAuthCompleteMessage {
  if (!data || typeof data !== 'object') {
    return false;
  }

  const message = data as Record<string, unknown>;
  return message['type'] === JIRA_OAUTH_MESSAGE_TYPE && typeof message['connected'] === 'boolean';
}

export function buildOAuthPopupFeatures(): string {
  const width = 560;
  const height = 720;
  const left = Math.max(0, window.screenX + (window.outerWidth - width) / 2);
  const top = Math.max(0, window.screenY + (window.outerHeight - height) / 2);

  return [
    `width=${width}`,
    `height=${height}`,
    `left=${left}`,
    `top=${top}`,
    'menubar=no',
    'toolbar=no',
    'location=yes',
    'status=no',
    'scrollbars=yes',
    'resizable=yes'
  ].join(',');
}
