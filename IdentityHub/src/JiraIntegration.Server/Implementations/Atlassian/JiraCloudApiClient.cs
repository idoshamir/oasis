using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JiraIntegration.Server.Models.Exceptions;

namespace JiraIntegration.Server.Implementations.Atlassian;

public sealed class JiraCloudApiClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<CreateMetaResponse> GetCreateMetaAsync(
        string cloudId,
        string accessToken,
        string? preferredProjectKey,
        CancellationToken cancellationToken)
    {
        var query = preferredProjectKey is null
            ? "expand=projects.issuetypes"
            : $"projectKeys={Uri.EscapeDataString(preferredProjectKey)}&expand=projects.issuetypes";

        var url = $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue/createmeta?{query}";
        var responseBody = await SendAuthorizedAsync(HttpMethod.Get, url, accessToken, content: null, cancellationToken);
        var createMeta = JsonSerializer.Deserialize<CreateMetaResponse>(responseBody, JsonOptions);
        if (createMeta is null)
        {
            throw new InvalidOperationException("Jira project metadata returned an invalid response.");
        }

        return createMeta;
    }

    public async Task<CreatedIssueResponse> CreateIssueAsync(
        string cloudId,
        string accessToken,
        CreateIssueRequest payload,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue";
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var responseBody = await SendAuthorizedAsync(
            HttpMethod.Post,
            url,
            accessToken,
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken,
            $"Jira issue creation failed");

        var created = JsonSerializer.Deserialize<CreatedIssueResponse>(responseBody, JsonOptions);
        if (created is null || string.IsNullOrWhiteSpace(created.Id) || string.IsNullOrWhiteSpace(created.Key))
        {
            throw new InvalidOperationException("Jira issue creation returned an invalid response.");
        }

        return created;
    }

    private async Task<string> SendAuthorizedAsync(
        HttpMethod method,
        string url,
        string accessToken,
        HttpContent? content,
        CancellationToken cancellationToken,
        string? failurePrefix = null)
    {
        var client = httpClientFactory.CreateClient("Atlassian");
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (content is not null)
        {
            request.Content = content;
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccessOrThrowPermission(
            response,
            failurePrefix is null
                ? $"Jira API request failed: {response.StatusCode} {responseBody}"
                : $"{failurePrefix}: {response.StatusCode} {responseBody}");

        return responseBody;
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

    public sealed record CreateMetaResponse
    {
        public List<CreateMetaProject> Projects { get; init; } = [];
    }

    public sealed record CreateMetaProject
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public List<CreateMetaIssueType> Issuetypes { get; init; } = [];
    }

    public sealed record CreateMetaIssueType
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record CreatedIssueResponse(string Id, string Key);

    public sealed record CreateIssueRequest(CreateIssueFields Fields);

    public sealed record CreateIssueFields(
        CreateIssueProject Project,
        string Summary,
        CreateIssueDescription Description,
        [property: JsonPropertyName("issuetype")] CreateIssueType IssueType);

    public sealed record CreateIssueProject(string Key);

    public sealed record CreateIssueType(string Name);

    public sealed record CreateIssueDescription(
        string Type,
        int Version,
        IReadOnlyList<CreateIssueDescriptionBlock> Content);

    public sealed record CreateIssueDescriptionBlock(
        string Type,
        IReadOnlyList<CreateIssueDescriptionText> Content);

    public sealed record CreateIssueDescriptionText(string Type, string Text);
}
