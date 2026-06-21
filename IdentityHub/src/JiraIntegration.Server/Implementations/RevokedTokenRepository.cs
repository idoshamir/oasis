using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Implementations;

public sealed class RevokedTokenRepository(AppDbContext dbContext) : IRevokedTokenRepository
{
    public Task<bool> IsRevokedAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.RevokedTokens
            .AsNoTracking()
            .AnyAsync(
                r => r.TokenHash == tokenHash && r.ExpiresAt > DateTimeOffset.UtcNow,
                cancellationToken);

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

    public Task DeleteExpiredAsync(CancellationToken cancellationToken = default) =>
        dbContext.RevokedTokens
            .Where(r => r.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
}
