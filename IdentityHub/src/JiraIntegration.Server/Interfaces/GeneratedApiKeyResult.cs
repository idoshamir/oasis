namespace JiraIntegration.Server.Interfaces;

public sealed record GeneratedApiKeyResult(
    Guid Id,
    string Name,
    string ProjectKey,
    string PlaintextKey,
    DateTimeOffset CreatedAt);
