using JiraIntegration.Server.Data.Entities;

namespace JiraIntegration.Server.Interfaces;

public interface IRefreshTokenRepository
{
    Task CreateAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetValidByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task RevokeByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task DeleteExpiredAsync(CancellationToken cancellationToken = default);
}
