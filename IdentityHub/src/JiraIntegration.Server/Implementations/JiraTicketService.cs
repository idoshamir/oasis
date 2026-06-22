using JiraIntegration.Server.Implementations.Atlassian;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Jira;

namespace JiraIntegration.Server.Implementations;

public sealed class JiraTicketService(JiraCloudApiClient jiraCloudApiClient) : IJiraTicketService
{
    private static readonly string[] PreferredIssueTypeNames = ["Task", "Story", "Bug"];

    public async Task<JiraProjectTarget> ResolveProjectTargetAsync(
        string cloudId,
        string accessToken,
        string? preferredProjectKey = null,
        CancellationToken cancellationToken = default)
    {
        var createMeta = await jiraCloudApiClient.GetCreateMetaAsync(
            cloudId,
            accessToken,
            preferredProjectKey,
            cancellationToken);

        if (createMeta.Projects.Count == 0)
        {
            throw new InvalidOperationException(
                "No Jira projects were found that allow issue creation for this account.");
        }

        foreach (var project in createMeta.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.Key))
            {
                continue;
            }

            if (preferredProjectKey is not null &&
                !project.Key.Equals(preferredProjectKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var issueTypeName = SelectIssueTypeName(project.Issuetypes);
            if (issueTypeName is not null)
            {
                return new JiraProjectTarget(project.Key, issueTypeName);
            }
        }

        if (preferredProjectKey is not null)
        {
            throw new InvalidOperationException(
                $"Configured Jira project '{preferredProjectKey}' was not found or you cannot create issues in it.");
        }

        throw new InvalidOperationException(
            "No Jira projects were found that allow issue creation for this account.");
    }

    public async Task<IReadOnlyList<JiraProjectOption>> GetCreatableProjectsAsync(
        string cloudId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var createMeta = await jiraCloudApiClient.GetCreateMetaAsync(
            cloudId,
            accessToken,
            preferredProjectKey: null,
            cancellationToken);

        return createMeta.Projects
            .Where(project => !string.IsNullOrWhiteSpace(project.Key))
            .Select(project =>
            {
                var issueTypeName = SelectIssueTypeName(project.Issuetypes);
                return issueTypeName is null
                    ? null
                    : new JiraProjectOption(
                        project.Key,
                        string.IsNullOrWhiteSpace(project.Name) ? project.Key : project.Name,
                        issueTypeName);
            })
            .Where(option => option is not null)
            .Select(option => option!)
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CreatedJiraIssue> CreateTaskAsync(
        string cloudId,
        string accessToken,
        string projectKey,
        string issueTypeName,
        string title,
        string description,
        CancellationToken cancellationToken = default)
    {
        var payload = new JiraCloudApiClient.CreateIssueRequest(
            new JiraCloudApiClient.CreateIssueFields(
                new JiraCloudApiClient.CreateIssueProject(projectKey),
                title,
                new JiraCloudApiClient.CreateIssueDescription(
                    "doc",
                    1,
                    [
                        new JiraCloudApiClient.CreateIssueDescriptionBlock(
                            "paragraph",
                            [new JiraCloudApiClient.CreateIssueDescriptionText("text", description)])
                    ]),
                new JiraCloudApiClient.CreateIssueType(issueTypeName)));

        var created = await jiraCloudApiClient.CreateIssueAsync(
            cloudId,
            accessToken,
            payload,
            cancellationToken);

        return new CreatedJiraIssue(created.Id, created.Key);
    }

    private static string? SelectIssueTypeName(IReadOnlyList<JiraCloudApiClient.CreateMetaIssueType> issueTypes)
    {
        if (issueTypes.Count == 0)
        {
            return null;
        }

        foreach (var preferredName in PreferredIssueTypeNames)
        {
            var match = issueTypes.FirstOrDefault(type =>
                preferredName.Equals(type.Name, StringComparison.OrdinalIgnoreCase));

            if (match?.Name is { Length: > 0 } name)
            {
                return name;
            }
        }

        return issueTypes.FirstOrDefault(type => !string.IsNullOrWhiteSpace(type.Name))?.Name;
    }
}
