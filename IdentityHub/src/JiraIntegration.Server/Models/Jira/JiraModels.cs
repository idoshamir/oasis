namespace JiraIntegration.Server.Models.Jira;

public sealed record JiraAuthUrlResponse(string Url);

public sealed record JiraConnectionStatusResponse(
    bool Connected,
    string? WorkspaceName,
    string? WorkspaceUrl);

public sealed record JiraCallbackQuery(string Code, string State);

public sealed record AtlassianTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);

public sealed record AtlassianAccessibleResource(
    string Id,
    string Name,
    string Url);

public sealed record CreatedJiraIssue(string Id, string Key);

public sealed record JiraProjectTarget(string ProjectKey, string IssueTypeName);

public sealed record JiraProjectOption(string Key, string Name, string IssueTypeName);

public sealed record JiraProjectsResponse(
    IReadOnlyList<JiraProjectOption> Projects,
    string? SelectedProjectKey);
