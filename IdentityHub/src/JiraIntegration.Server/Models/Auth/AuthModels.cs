namespace JiraIntegration.Server.Models.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAt);
