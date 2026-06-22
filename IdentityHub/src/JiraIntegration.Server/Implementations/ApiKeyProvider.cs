using System.Security.Claims;
using AspNetCore.Authentication.ApiKey;
using JiraIntegration.Server.Auth;
using JiraIntegration.Server.Interfaces;

namespace JiraIntegration.Server.Implementations;

public sealed class ApiKeyProvider(
    IApiKeyHasher apiKeyHasher,
    IApiKeyRepository apiKeyRepository) : IApiKeyProvider
{
    public async Task<IApiKey?> ProvideAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var keyHash = apiKeyHasher.HashApiKey(key);
        var apiKey = await apiKeyRepository.GetActiveByKeyHashAsync(keyHash);
        if (apiKey is null)
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKey.UserId.ToString()),
            new("userId", apiKey.UserId.ToString()),
            new("apiKeyId", apiKey.Id.ToString())
        };

        if (!string.IsNullOrWhiteSpace(apiKey.ProjectKey))
        {
            claims.Add(new Claim("projectKey", apiKey.ProjectKey));
        }

        return new ApiKeyDetails(
            key,
            apiKey.UserId.ToString(),
            claims.AsReadOnly(),
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            null);
    }

    private sealed class ApiKeyDetails(
        string key,
        string owner,
        IReadOnlyCollection<Claim> claims,
        string scheme,
        string? additionalData) : IApiKey
    {
        public string Key { get; } = key;
        public string OwnerName { get; } = owner;
        public IReadOnlyCollection<Claim> Claims { get; } = claims;
        public string Scheme { get; } = scheme;
        public string? AdditionalData { get; } = additionalData;
    }
}
