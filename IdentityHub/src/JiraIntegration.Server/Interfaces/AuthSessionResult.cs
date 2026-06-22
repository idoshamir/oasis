namespace JiraIntegration.Server.Interfaces;

public sealed record AuthSessionResult(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken);
