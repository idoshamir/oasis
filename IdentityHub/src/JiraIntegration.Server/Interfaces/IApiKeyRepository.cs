using JiraIntegration.Server.Data.Entities;

namespace JiraIntegration.Server.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetActiveByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiKey> CreateAsync(
        Guid userId,
        string keyHash,
        string? keyPrefix,
        string name,
        string projectKey,
        CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid userId, Guid keyId, CancellationToken cancellationToken = default);
    Task<ApiKey?> GetActiveByUserIdAndProjectKeyAsync(
        Guid userId,
        string projectKey,
        CancellationToken cancellationToken = default);
}
