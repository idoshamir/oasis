namespace JiraIntegration.Server.Data.Entities;

public sealed class RefreshToken
{
    public string TokenHash { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public User User { get; set; } = null!;
}
