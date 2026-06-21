using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Implementations;

public sealed class ApiKeyRepository(AppDbContext dbContext) : IApiKeyRepository
{
    public Task<ApiKey?> GetActiveByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default) =>
        dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive, cancellationToken);

    public async Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // SQLite cannot translate DateTimeOffset in ORDER BY; sort in memory after filtering.
        var keys = await dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId && k.IsActive)
            .ToListAsync(cancellationToken);

        return keys
            .OrderByDescending(k => k.CreatedAt)
            .ToList();
    }

    public async Task<ApiKey> CreateAsync(
        Guid userId,
        string keyHash,
        string? keyPrefix,
        string name,
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Name = name,
            ProjectKey = projectKey,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);
        return apiKey;
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid keyId, CancellationToken cancellationToken = default)
    {
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId && k.IsActive, cancellationToken);

        if (apiKey is null)
        {
            return false;
        }

        apiKey.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<ApiKey?> GetActiveByUserIdAndProjectKeyAsync(
        Guid userId,
        string projectKey,
        CancellationToken cancellationToken = default) =>
        dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                k => k.UserId == userId && k.ProjectKey == projectKey && k.IsActive,
                cancellationToken);
}
