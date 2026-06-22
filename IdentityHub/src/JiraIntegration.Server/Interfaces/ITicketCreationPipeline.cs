using JiraIntegration.Server.Models.Jira;
using JiraIntegration.Server.Models.Tickets;

namespace JiraIntegration.Server.Interfaces;

public interface ITicketCreationPipeline
{
    Task<JiraProjectsResponse?> GetCreatableProjectsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<TicketCreationResult> CreateTicketAsync(
        Guid userId,
        string title,
        string description,
        string? projectKey = null,
        CancellationToken cancellationToken = default);

    Task<RecentTicketsResponse?> GetRecentTicketsAsync(
        Guid userId,
        int count = 10,
        string? projectKey = null,
        CancellationToken cancellationToken = default);
}
