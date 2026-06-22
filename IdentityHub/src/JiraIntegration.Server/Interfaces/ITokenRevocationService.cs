namespace JiraIntegration.Server.Interfaces;

public interface ITokenRevocationService
{
    Task RevokeAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<bool> IsRevokedAsync(string accessToken, CancellationToken cancellationToken = default);
}
