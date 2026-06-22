using System.Security.Cryptography;
using JiraIntegration.Server.Interfaces;

namespace JiraIntegration.Server.Implementations;

public sealed class ApiKeyService(
    IApiKeyRepository apiKeyRepository,
    IApiKeyHasher apiKeyHasher) : IApiKeyService
{
    private const string KeyPrefix = "ih-";
    private const int KeyPrefixDisplayLength = 8;

    public async Task<IReadOnlyList<ApiKeyListItem>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var keys = await apiKeyRepository.GetByUserIdAsync(userId, cancellationToken);
        return keys
            .Select(k => new ApiKeyListItem(
                k.Id,
                k.Name,
                k.ProjectKey,
                FormatMaskedKey(k.KeyPrefix),
                k.CreatedAt))
            .ToList();
    }

    public async Task<GeneratedApiKeyResult> GenerateAsync(
        Guid userId,
        string name,
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        var plaintextKey = GenerateApiKey();
        var keyHash = apiKeyHasher.HashApiKey(plaintextKey);
        var keyPrefix = plaintextKey.Length >= KeyPrefixDisplayLength
            ? plaintextKey[..KeyPrefixDisplayLength]
            : plaintextKey;

        var apiKey = await apiKeyRepository.CreateAsync(
            userId,
            keyHash,
            keyPrefix,
            name,
            projectKey,
            cancellationToken);

        return new GeneratedApiKeyResult(
            apiKey.Id,
            apiKey.Name,
            apiKey.ProjectKey,
            plaintextKey,
            apiKey.CreatedAt);
    }

    public Task<bool> RevokeAsync(Guid userId, Guid keyId, CancellationToken cancellationToken = default) =>
        apiKeyRepository.RevokeAsync(userId, keyId, cancellationToken);

    public async Task<GeneratedApiKeyResult> RegenerateAsync(
        Guid userId,
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        var existing = await apiKeyRepository.GetActiveByUserIdAndProjectKeyAsync(
            userId,
            projectKey,
            cancellationToken);

        if (existing is not null)
        {
            await apiKeyRepository.RevokeAsync(userId, existing.Id, cancellationToken);
        }

        return await GenerateAsync(userId, $"{projectKey} API Key", projectKey, cancellationToken);
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var base64Url = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{KeyPrefix}{base64Url}";
    }

    private static string FormatMaskedKey(string? keyPrefix) =>
        string.IsNullOrWhiteSpace(keyPrefix) ? "ih-..." : $"{keyPrefix}...";
}
