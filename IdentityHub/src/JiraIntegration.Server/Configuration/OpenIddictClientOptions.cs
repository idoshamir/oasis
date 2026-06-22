namespace JiraIntegration.Server.Configuration;

public sealed class OpenIddictClientOptions
{
    public const string SectionName = "OpenIddict";

    public string ClientId { get; set; } = "jira-integration";
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        ClientSecret.Length >= 32;
}
