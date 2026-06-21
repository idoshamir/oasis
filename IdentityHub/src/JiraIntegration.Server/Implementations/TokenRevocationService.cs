using System.Security.Cryptography;
using System.Text;
using JiraIntegration.Server.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace JiraIntegration.Server.Implementations;

public sealed class TokenRevocationService(IMemoryCache memoryCache) : ITokenRevocationService
{
    private const string CachePrefix = "revoked-token:";

    public Task RevokeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.CompletedTask;
        }

        var ttl = GetRemainingLifetime(accessToken);
        if (ttl <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        memoryCache.Set(GetCacheKey(accessToken), true, ttl);
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(memoryCache.TryGetValue(GetCacheKey(accessToken), out _));
    }

    private static string GetCacheKey(string accessToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
        return $"{CachePrefix}{Convert.ToBase64String(hash)}";
    }

    private static TimeSpan GetRemainingLifetime(string accessToken)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.ValidTo - DateTime.UtcNow;
    }
}
