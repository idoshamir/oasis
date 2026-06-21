using System.Collections.Concurrent;
using System.Security.Cryptography;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace JiraIntegration.Server.Implementations;

public sealed class JiraTokenRefreshService(
    IServiceScopeFactory scopeFactory,
    ILogger<JiraTokenRefreshService> logger) : IJiraTokenRefreshService
{
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _refreshLocks = new();

    public async Task<string> GetValidAccessTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default,
        bool forceRefresh = false)
    {
        try
        {
            return await GetValidAccessTokenCoreAsync(userId, cancellationToken, forceRefresh);
        }
        catch (CryptographicException ex)
        {
            await ClearInvalidConnectionAsync(userId, cancellationToken);
            logger.LogWarning(ex, "Failed to decrypt Jira tokens for user {UserId}", userId);
            throw new JiraNotConnectedException(
                "Jira connection is invalid. Please reconnect your Jira workspace.");
        }
    }

    private async Task<string> GetValidAccessTokenCoreAsync(
        Guid userId,
        CancellationToken cancellationToken,
        bool forceRefresh)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var connectionValidator = scope.ServiceProvider.GetRequiredService<IJiraConnectionValidator>();
        var connection = await connectionValidator.GetUsableAsync(userId, cancellationToken);
        if (connection is null)
        {
            throw new JiraNotConnectedException();
        }

        var tokenEncryptionService = scope.ServiceProvider.GetRequiredService<ITokenEncryptionService>();
        if (!forceRefresh && connection.ExpiresAt > DateTimeOffset.UtcNow.Add(RefreshBuffer))
        {
            return tokenEncryptionService.Decrypt(connection.EncryptedAccessToken);
        }

        var refreshLock = _refreshLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            await using var refreshScope = scopeFactory.CreateAsyncScope();
            var refreshValidator = refreshScope.ServiceProvider.GetRequiredService<IJiraConnectionValidator>();
            var refreshConnection = await refreshValidator.GetUsableAsync(userId, cancellationToken);
            if (refreshConnection is null)
            {
                throw new JiraNotConnectedException();
            }

            var encryptionService = refreshScope.ServiceProvider.GetRequiredService<ITokenEncryptionService>();
            if (!forceRefresh && refreshConnection.ExpiresAt > DateTimeOffset.UtcNow.Add(RefreshBuffer))
            {
                return encryptionService.Decrypt(refreshConnection.EncryptedAccessToken);
            }

            var oauthService = refreshScope.ServiceProvider.GetRequiredService<IJiraOAuthService>();
            var connectionRepository = refreshScope.ServiceProvider.GetRequiredService<IJiraConnectionRepository>();

            var refreshToken = encryptionService.Decrypt(refreshConnection.EncryptedRefreshToken);
            var refreshed = await oauthService.RefreshAccessTokenAsync(refreshToken, cancellationToken);

            refreshConnection.EncryptedAccessToken = encryptionService.Encrypt(refreshed.AccessToken);
            refreshConnection.EncryptedRefreshToken = encryptionService.Encrypt(refreshed.RefreshToken);
            refreshConnection.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn);
            await connectionRepository.SaveAsync(refreshConnection, cancellationToken);

            logger.LogInformation("Refreshed Jira access token for user {UserId}", userId);
            return refreshed.AccessToken;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task ClearInvalidConnectionAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var connectionRepository = scope.ServiceProvider.GetRequiredService<IJiraConnectionRepository>();
        await connectionRepository.DeleteByUserIdAsync(userId, cancellationToken);
    }
}
