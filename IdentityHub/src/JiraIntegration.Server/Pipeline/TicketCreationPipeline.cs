using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Exceptions;
using JiraIntegration.Server.Models.Jira;
using JiraIntegration.Server.Models.Tickets;
using Microsoft.Extensions.Logging;

namespace JiraIntegration.Server.Pipeline;

public sealed class TicketCreationPipeline(
    IJiraConnectionRepository jiraConnectionRepository,
    IJiraConnectionValidator jiraConnectionValidator,
    IJiraTokenRefreshService jiraTokenRefreshService,
    IJiraTicketService jiraTicketService,
    INhiTicketLedgerRepository ledgerRepository,
    ILogger<TicketCreationPipeline> logger) : ITicketCreationPipeline
{
    public async Task<JiraProjectsResponse?> GetCreatableProjectsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var connection = await jiraConnectionValidator.GetUsableAsync(userId, cancellationToken);
        if (connection is null)
        {
            return null;
        }

        var projects = await GetCreatableProjectsWithTokenRefreshAsync(userId, connection, cancellationToken);

        var selectedProjectKey = string.IsNullOrWhiteSpace(connection.DefaultProjectKey)
            ? null
            : connection.DefaultProjectKey;

        return new JiraProjectsResponse(projects, selectedProjectKey);
    }

    public async Task<RecentTicketsResponse?> GetRecentTicketsAsync(
        Guid userId,
        int count = 10,
        string? projectKey = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await jiraConnectionRepository.GetByUserIdAsync(userId, cancellationToken);
        var entries = await ledgerRepository.GetRecentByUserIdAsync(
            userId,
            count,
            projectKey,
            cancellationToken);

        var tickets = entries
            .Select(entry => new RecentTicketItem(
                entry.JiraIssueKey,
                entry.Title,
                entry.CreatedAt,
                connection is null
                    ? null
                    : BuildIssueBrowseUrl(connection.WorkspaceUrl, entry.JiraIssueKey)))
            .ToList();

        return new RecentTicketsResponse(tickets);
    }

    public async Task<TicketCreationResult> CreateTicketAsync(
        Guid userId,
        string title,
        string description,
        string? projectKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.");
        }

        var connection = await jiraConnectionValidator.GetUsableAsync(userId, cancellationToken);
        if (connection is null)
        {
            logger.LogWarning("Ticket creation failed for user {UserId}: Jira not connected", userId);
            throw new JiraNotConnectedException();
        }

        var accessToken = await jiraTokenRefreshService.GetValidAccessTokenAsync(userId, cancellationToken);
        var projectTarget = await ResolveProjectTargetAsync(connection, accessToken, projectKey, cancellationToken);

        if (!projectTarget.ProjectKey.Equals(connection.DefaultProjectKey, StringComparison.OrdinalIgnoreCase) ||
            !projectTarget.IssueTypeName.Equals(connection.DefaultIssueTypeName, StringComparison.OrdinalIgnoreCase))
        {
            connection.DefaultProjectKey = projectTarget.ProjectKey;
            connection.DefaultIssueTypeName = projectTarget.IssueTypeName;
            await jiraConnectionRepository.SaveAsync(connection, cancellationToken);
        }

        CreatedJiraIssue created;
        try
        {
            created = await jiraTicketService.CreateTaskAsync(
                connection.AtlassianCloudId,
                accessToken,
                projectTarget.ProjectKey,
                projectTarget.IssueTypeName,
                title.Trim(),
                description ?? string.Empty,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(
                ex,
                "Jira issue creation failed for user {UserId} in project {ProjectKey} with issue type {IssueTypeName}",
                userId,
                projectTarget.ProjectKey,
                projectTarget.IssueTypeName);
            throw;
        }

        var ledgerEntry = new NhiTicketLedger
        {
            UserId = userId,
            JiraIssueId = created.Id,
            JiraIssueKey = created.Key,
            Title = title.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        var saved = await ledgerRepository.AddAsync(ledgerEntry, cancellationToken);
        logger.LogInformation(
            "Created Jira issue {IssueKey} for user {UserId}",
            created.Key,
            userId);

        var browseUrl = BuildIssueBrowseUrl(connection.WorkspaceUrl, created.Key);

        return new TicketCreationResult(
            created.Key,
            created.Id,
            saved.Title,
            saved.Id,
            saved.CreatedAt,
            browseUrl);
    }

    private static string BuildIssueBrowseUrl(string workspaceUrl, string issueKey) =>
        $"{workspaceUrl.TrimEnd('/')}/browse/{issueKey}";

    private async Task<IReadOnlyList<JiraProjectOption>> GetCreatableProjectsWithTokenRefreshAsync(
        Guid userId,
        JiraConnection connection,
        CancellationToken cancellationToken)
    {
        var accessToken = await jiraTokenRefreshService.GetValidAccessTokenAsync(userId, cancellationToken);
        try
        {
            return await jiraTicketService.GetCreatableProjectsAsync(
                connection.AtlassianCloudId,
                accessToken,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            accessToken = await jiraTokenRefreshService.GetValidAccessTokenAsync(
                userId,
                cancellationToken,
                forceRefresh: true);
            return await jiraTicketService.GetCreatableProjectsAsync(
                connection.AtlassianCloudId,
                accessToken,
                cancellationToken);
        }
    }

    private async Task<JiraProjectTarget> ResolveProjectTargetAsync(
        JiraConnection connection,
        string accessToken,
        string? projectKey,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(projectKey))
        {
            return await jiraTicketService.ResolveProjectTargetAsync(
                connection.AtlassianCloudId,
                accessToken,
                projectKey.Trim(),
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(connection.DefaultProjectKey))
        {
            return new JiraProjectTarget(
                connection.DefaultProjectKey,
                string.IsNullOrWhiteSpace(connection.DefaultIssueTypeName)
                    ? "Task"
                    : connection.DefaultIssueTypeName);
        }

        return await jiraTicketService.ResolveProjectTargetAsync(
            connection.AtlassianCloudId,
            accessToken,
            preferredProjectKey: null,
            cancellationToken);
    }
}
