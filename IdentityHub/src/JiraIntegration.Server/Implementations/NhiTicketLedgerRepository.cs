using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Implementations;

public sealed class NhiTicketLedgerRepository(AppDbContext dbContext) : INhiTicketLedgerRepository
{
    public async Task<NhiTicketLedger> AddAsync(NhiTicketLedger entry, CancellationToken cancellationToken = default)
    {
        if (entry.Id == Guid.Empty)
        {
            entry.Id = Guid.NewGuid();
        }

        if (entry.CreatedAt == default)
        {
            entry.CreatedAt = DateTimeOffset.UtcNow;
        }

        dbContext.NhiTicketLedgers.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<IReadOnlyList<NhiTicketLedger>> GetRecentByUserIdAsync(
        Guid userId,
        int count,
        string? projectKey = null,
        CancellationToken cancellationToken = default)
    {
        // SQLite cannot translate DateTimeOffset in ORDER BY; sort in memory after filtering.
        var entries = await dbContext.NhiTicketLedgers
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .ToListAsync(cancellationToken);

        IEnumerable<NhiTicketLedger> filtered = entries;
        if (!string.IsNullOrWhiteSpace(projectKey))
        {
            var prefix = $"{projectKey.Trim()}-";
            filtered = entries.Where(entry =>
                entry.JiraIssueKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .OrderByDescending(l => l.CreatedAt)
            .Take(count)
            .ToList();
    }
}
