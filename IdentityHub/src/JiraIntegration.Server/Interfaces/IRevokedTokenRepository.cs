namespace JiraIntegration.Server.Interfaces;

public interface IRevokedTokenRepository
{
    Task<bool> IsRevokedAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task RevokeAsync(string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task DeleteExpiredAsync(CancellationToken cancellationToken = default);
}
