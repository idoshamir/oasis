namespace JiraIntegration.Server.Models.Nhi;

public sealed record CreateNhiFindingRequest(string Title, string Description, string ProjectKey);

public sealed record NhiFindingResponse(string IssueKey, string IssueId, Guid LedgerId);
