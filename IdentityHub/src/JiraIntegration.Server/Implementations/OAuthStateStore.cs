using System.Security.Cryptography;
using JiraIntegration.Server.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace JiraIntegration.Server.Implementations;

public sealed class OAuthStateStore(IMemoryCache memoryCache) : IOAuthStateStore
{
    private const string CachePrefix = "oauth-state:";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    public string CreateState(Guid userId)
    {
        var stateId = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        memoryCache.Set(GetCacheKey(stateId), userId, StateLifetime);
        return stateId;
    }

    public Guid? ValidateAndConsume(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var cacheKey = GetCacheKey(state);
        if (!memoryCache.TryGetValue(cacheKey, out Guid userId))
        {
            return null;
        }

        memoryCache.Remove(cacheKey);
        return userId;
    }

    private static string GetCacheKey(string stateId) => $"{CachePrefix}{stateId}";
}
