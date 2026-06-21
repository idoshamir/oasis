using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Exceptions;
using JiraIntegration.Server.Models.Jira;

namespace JiraIntegration.Server.Implementations;

public sealed class JiraTicketService(
    IHttpClientFactory httpClientFactory) : IJiraTicketService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] PreferredIssueTypeNames = ["Task", "Story", "Bug"];

    public async Task<JiraProjectTarget> ResolveProjectTargetAsync(
        string cloudId,
        string accessToken,
        string? preferredProjectKey = null,
        CancellationToken cancellationToken = default)
    {
        var createMeta = await GetCreateMetaAsync(cloudId, accessToken, preferredProjectKey, cancellationToken);
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
        var createMeta = await GetCreateMetaAsync(cloudId, accessToken, preferredProjectKey: null, cancellationToken);

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
        var client = httpClientFactory.CreateClient("Atlassian");
        var url = $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue";

        var payload = new
        {
            fields = new
            {
                project = new { key = projectKey },
                summary = title,
                description = new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                        new
                        {
                            type = "paragraph",
                            content = new[]
                            {
                                new { type = "text", text = description }
                            }
                        }
                    }
                },
                issuetype = new { name = issueTypeName }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        EnsureSuccessOrThrowPermission(response, $"Jira issue creation failed: {response.StatusCode} {responseBody}");

        var created = JsonSerializer.Deserialize<CreatedJiraIssueDto>(responseBody, JsonOptions);
        if (created is null || string.IsNullOrWhiteSpace(created.Id) || string.IsNullOrWhiteSpace(created.Key))
        {
            throw new InvalidOperationException("Jira issue creation returned an invalid response.");
        }

        return new CreatedJiraIssue(created.Id, created.Key);
    }

    private async Task<CreateMetaResponse> GetCreateMetaAsync(
        string cloudId,
        string accessToken,
        string? preferredProjectKey,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Atlassian");
        var query = preferredProjectKey is null
            ? "expand=projects.issuetypes"
            : $"projectKeys={Uri.EscapeDataString(preferredProjectKey)}&expand=projects.issuetypes";

        var url = $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue/createmeta?{query}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        EnsureSuccessOrThrowPermission(
            response,
            $"Failed to load Jira project metadata: {response.StatusCode} {responseBody}");

        var createMeta = JsonSerializer.Deserialize<CreateMetaResponse>(responseBody, JsonOptions);
        if (createMeta is null)
        {
            throw new InvalidOperationException("Jira project metadata returned an invalid response.");
        }

        return createMeta;
    }

    private static void EnsureSuccessOrThrowPermission(HttpResponseMessage response, string failureMessage)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new AtlassianPermissionException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }

    private static string? SelectIssueTypeName(IReadOnlyList<CreateMetaIssueType> issueTypes)
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

    private sealed record CreatedJiraIssueDto(string Id, string Key);

    private sealed record CreateMetaResponse
    {
        public List<CreateMetaProject> Projects { get; init; } = [];
    }

    private sealed record CreateMetaProject
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public List<CreateMetaIssueType> Issuetypes { get; init; } = [];
    }

    private sealed record CreateMetaIssueType
    {
        public string Name { get; init; } = string.Empty;
    }
}
