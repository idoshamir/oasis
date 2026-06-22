using JiraIntegration.Server.Models.Jira;

namespace JiraIntegration.Server.Interfaces;

public interface IJiraTicketService
{
    Task<JiraProjectTarget> ResolveProjectTargetAsync(
        string cloudId,
        string accessToken,
        string? preferredProjectKey = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraProjectOption>> GetCreatableProjectsAsync(
        string cloudId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<CreatedJiraIssue> CreateTaskAsync(
        string cloudId,
        string accessToken,
        string projectKey,
        string issueTypeName,
        string title,
        string description,
        CancellationToken cancellationToken = default);
}
