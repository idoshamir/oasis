using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Implementations;

public sealed class RefreshTokenRepository(AppDbContext dbContext) : IRefreshTokenRepository
{
    public async Task CreateAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = tokenHash,
            UserId = userId,
            ExpiresAt = expiresAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshToken?> GetValidByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

        return entry is not null && entry.ExpiresAt > DateTimeOffset.UtcNow
            ? entry
            : null;
    }

    public async Task RevokeByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);
        if (entry is null)
        {
            return;
        }

        dbContext.RefreshTokens.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var all = await dbContext.RefreshTokens.ToListAsync(cancellationToken);
        var expired = all.Where(r => r.ExpiresAt <= now).ToList();

        if (expired.Count == 0)
        {
            return;
        }

        dbContext.RefreshTokens.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
