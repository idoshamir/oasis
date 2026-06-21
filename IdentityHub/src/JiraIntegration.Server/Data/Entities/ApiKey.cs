namespace JiraIntegration.Server.Data.Entities;

public sealed class ApiKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string? KeyPrefix { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProjectKey { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
