namespace JiraIntegration.Server.Interfaces;

public sealed record AuthTokenResult(string Token, DateTimeOffset ExpiresAt);
