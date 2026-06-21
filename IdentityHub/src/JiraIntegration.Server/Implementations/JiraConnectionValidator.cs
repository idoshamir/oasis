using System.Security.Cryptography;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;

namespace JiraIntegration.Server.Implementations;

public sealed class JiraConnectionValidator(
    IJiraConnectionRepository jiraConnectionRepository,
    ITokenEncryptionService tokenEncryptionService,
    ILogger<JiraConnectionValidator> logger) : IJiraConnectionValidator
{
    public async Task<JiraConnection?> GetUsableAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connection = await jiraConnectionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (connection is null)
        {
            return null;
        }

        if (CanDecrypt(connection.EncryptedAccessToken) && CanDecrypt(connection.EncryptedRefreshToken))
        {
            return connection;
        }

        logger.LogWarning(
            "Stored Jira tokens for user {UserId} cannot be decrypted. Clearing connection so the user can reconnect.",
            userId);
        await jiraConnectionRepository.DeleteByUserIdAsync(userId, cancellationToken);
        return null;
    }

    private bool CanDecrypt(string ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return false;
        }

        try
        {
            tokenEncryptionService.Decrypt(ciphertext);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
