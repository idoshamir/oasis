using JiraIntegration.Server.Data.Entities;

namespace JiraIntegration.Server.Interfaces;

public interface INhiTicketLedgerRepository
{
    Task<NhiTicketLedger> AddAsync(NhiTicketLedger entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NhiTicketLedger>> GetRecentByUserIdAsync(
        Guid userId,
        int count,
        string? projectKey = null,
        CancellationToken cancellationToken = default);
}
