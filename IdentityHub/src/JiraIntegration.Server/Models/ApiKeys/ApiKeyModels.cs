namespace JiraIntegration.Server.Models.ApiKeys;

public sealed record GenerateApiKeyRequest(string Name, string ProjectKey);

public sealed record RegenerateApiKeyRequest(string ProjectKey);

public sealed record ApiKeyListItemResponse(
    Guid Id,
    string Name,
    string ProjectKey,
    string MaskedKey,
    DateTimeOffset CreatedAt);

public sealed record GenerateApiKeyResponse(
    Guid Id,
    string Name,
    string ProjectKey,
    string PlaintextKey,
    DateTimeOffset CreatedAt);

public sealed record ApiKeyListResponse(IReadOnlyList<ApiKeyListItemResponse> Keys);
