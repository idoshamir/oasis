namespace JiraIntegration.Server.Interfaces;

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyListItem>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<GeneratedApiKeyResult> GenerateAsync(
        Guid userId,
        string name,
        string projectKey,
        CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid userId, Guid keyId, CancellationToken cancellationToken = default);
    Task<GeneratedApiKeyResult> RegenerateAsync(
        Guid userId,
        string projectKey,
        CancellationToken cancellationToken = default);
}
