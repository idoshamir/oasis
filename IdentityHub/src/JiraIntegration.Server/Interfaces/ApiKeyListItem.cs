namespace JiraIntegration.Server.Interfaces;

public sealed record ApiKeyListItem(
    Guid Id,
    string Name,
    string ProjectKey,
    string MaskedKey,
    DateTimeOffset CreatedAt);
