namespace JiraIntegration.Server.Configuration;

public sealed class AtlassianOptions
{
    public const string SectionName = "Atlassian";

    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string Scopes { get; init; } = string.Empty;
    public string FrontendSuccessUrl { get; init; } = string.Empty;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !IsPlaceholder(ClientId) &&
        !IsPlaceholder(ClientSecret);

    private static bool IsPlaceholder(string value) =>
        value.Contains("your-atlassian", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("paste_your", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("replace-me", StringComparison.OrdinalIgnoreCase);
}
