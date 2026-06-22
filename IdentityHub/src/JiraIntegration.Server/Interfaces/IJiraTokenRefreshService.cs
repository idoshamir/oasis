namespace JiraIntegration.Server.Interfaces;

public interface IJiraTokenRefreshService
{
    Task<string> GetValidAccessTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false);
}
