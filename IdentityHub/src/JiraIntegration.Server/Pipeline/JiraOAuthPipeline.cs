using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.Extensions.Options;

namespace JiraIntegration.Server.Pipeline;

public sealed class JiraOAuthPipeline(
    IOAuthStateStore oauthStateStore,
    IUserRepository userRepository,
    IJiraOAuthService jiraOAuthService,
    IJiraConnectionRepository jiraConnectionRepository,
    ITokenEncryptionService tokenEncryptionService,
    IOptions<AtlassianOptions> atlassianOptions) : IJiraOAuthPipeline
{
    public async Task<string> CompleteOAuthAsync(
        string code,
        string state,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            throw new InvalidOperationException("Missing OAuth callback parameters.");
        }

        var userId = oauthStateStore.ValidateAndConsume(state);
        if (userId is null)
        {
            throw new InvalidOperationException("Invalid or expired OAuth state.");
        }

        var user = await userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException(
                "OAuth session user no longer exists. Sign out, sign in again, and retry connecting Jira.");
        }

        var tokens = await jiraOAuthService.ExchangeCodeAsync(code, cancellationToken);
        var resource = await jiraOAuthService.GetPrimaryAccessibleResourceAsync(tokens.AccessToken, cancellationToken);

        var connection = new JiraConnection
        {
            UserId = userId.Value,
            AtlassianCloudId = resource.Id,
            WorkspaceName = resource.Name ?? string.Empty,
            WorkspaceUrl = resource.Url ?? string.Empty,
            EncryptedAccessToken = tokenEncryptionService.Encrypt(tokens.AccessToken),
            EncryptedRefreshToken = tokenEncryptionService.Encrypt(tokens.RefreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn)
        };

        await jiraConnectionRepository.SaveAsync(connection, cancellationToken);
        return atlassianOptions.Value.FrontendSuccessUrl;
    }
}
