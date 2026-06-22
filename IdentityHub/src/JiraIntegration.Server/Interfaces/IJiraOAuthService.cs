using JiraIntegration.Server.Models.Jira;

namespace JiraIntegration.Server.Interfaces;

public interface IJiraOAuthService
{
    string BuildAuthorizationUrl(string state);
    Task<AtlassianTokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<AtlassianTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
    Task<AtlassianAccessibleResource> GetPrimaryAccessibleResourceAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}
