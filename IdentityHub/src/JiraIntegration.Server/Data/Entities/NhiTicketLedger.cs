namespace JiraIntegration.Server.Data.Entities;

public sealed class NhiTicketLedger
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string JiraIssueId { get; set; } = string.Empty;
    public string JiraIssueKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
