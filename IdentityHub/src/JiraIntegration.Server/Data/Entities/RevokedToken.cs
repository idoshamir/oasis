namespace JiraIntegration.Server.Data.Entities;

public sealed class RevokedToken
{
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
