namespace JiraIntegration.Server.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Secret { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = 60;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Secret) &&
        Secret.Length >= 32;
}
