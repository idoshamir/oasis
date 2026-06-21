using JiraIntegration.Server.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace JiraIntegration.Server.Implementations;

public sealed class OAuthStateStore(IMemoryCache memoryCache) : IOAuthStateStore
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(5);
    private const string CachePrefix = "oauth-state:";

    public string CreateState(Guid userId)
    {
        var state = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        memoryCache.Set($"{CachePrefix}{state}", userId, StateLifetime);
        return state;
    }

    public Guid? ValidateAndConsume(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var cacheKey = $"{CachePrefix}{state}";
        if (!memoryCache.TryGetValue(cacheKey, out Guid userId))
        {
            return null;
        }

        memoryCache.Remove(cacheKey);
        return userId;
    }
}
