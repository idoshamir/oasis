using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Implementations;

public sealed class RevokedTokenRepository(AppDbContext dbContext) : IRevokedTokenRepository
{
    public async Task<bool> IsRevokedAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.RevokedTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

        return entry is not null && entry.ExpiresAt > DateTimeOffset.UtcNow;
    }

    public async Task RevokeAsync(
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        var exists = await dbContext.RevokedTokens
            .AsNoTracking()
            .AnyAsync(r => r.TokenHash == tokenHash, cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.RevokedTokens.Add(new RevokedToken
        {
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        // SQLite cannot translate DateTimeOffset in WHERE; filter in memory after loading.
        var now = DateTimeOffset.UtcNow;
        var all = await dbContext.RevokedTokens.ToListAsync(cancellationToken);
        var expired = all.Where(r => r.ExpiresAt <= now).ToList();

        if (expired.Count == 0)
        {
            return;
        }

        dbContext.RevokedTokens.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
