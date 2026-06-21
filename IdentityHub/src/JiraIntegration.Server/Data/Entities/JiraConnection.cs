namespace JiraIntegration.Server.Data.Entities;

public sealed class JiraConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string AtlassianCloudId { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string WorkspaceUrl { get; set; } = string.Empty;
    public string DefaultProjectKey { get; set; } = string.Empty;
    public string DefaultIssueTypeName { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }

    public User User { get; set; } = null!;
}
