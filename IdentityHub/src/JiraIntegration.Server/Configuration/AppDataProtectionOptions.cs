namespace JiraIntegration.Server.Configuration;

public sealed class AppDataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public string KeysPath { get; init; } = "data-protection-keys";
}
