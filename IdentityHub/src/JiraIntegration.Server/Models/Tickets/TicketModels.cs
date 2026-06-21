namespace JiraIntegration.Server.Models.Tickets;

public sealed record CreateUiTicketRequest(string ProjectKey, string Title, string Description);

public sealed record UiTicketResponse(
    string IssueKey,
    string IssueId,
    string Title,
    DateTimeOffset CreatedAt);

public sealed record RecentTicketItem(
    string IssueKey,
    string Title,
    DateTimeOffset CreatedAt,
    string? ExternalUrl);

public sealed record RecentTicketsResponse(IReadOnlyList<RecentTicketItem> Tickets);

public sealed record TicketCreationResult(
    string IssueKey,
    string IssueId,
    string Title,
    Guid LedgerId,
    DateTimeOffset CreatedAt,
    string IssueBrowseUrl);
