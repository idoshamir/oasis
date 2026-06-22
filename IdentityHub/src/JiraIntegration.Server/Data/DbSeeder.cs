using System.Security.Cryptography;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JiraIntegration.Server.Data;

public sealed class DbSeeder(
    IUserRepository userRepository,
    IApiKeyRepository apiKeyRepository,
    UserManager<User> userManager,
    IApiKeyHasher apiKeyHasher,
    IHostEnvironment hostEnvironment,
    ILogger<DbSeeder> logger)
{
    private const string UsernameEnvVar = "SEED_USERNAME";
    private const string PasswordEnvVar = "SEED_PASSWORD";
    private const string ApiKeyNameEnvVar = "SEED_API_KEY_NAME";
    private const string DefaultApiKeyName = "BlogScanner dev key";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var username = Environment.GetEnvironmentVariable(UsernameEnvVar);
        var password = Environment.GetEnvironmentVariable(PasswordEnvVar);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            if (hostEnvironment.IsDevelopment())
            {
                logger.LogInformation(
                    "Skipping database seed. Set {UsernameVar} and {PasswordVar} environment variables to enable.",
                    UsernameEnvVar,
                    PasswordEnvVar);
            }

            return;
        }

        if (await userRepository.UsernameExistsAsync(username, cancellationToken))
        {
            return;
        }

        var apiKeyName = Environment.GetEnvironmentVariable(ApiKeyNameEnvVar)
            ?? DefaultApiKeyName;

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = username,
            NormalizedUserName = userManager.NormalizeName(username)
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            logger.LogWarning("Failed to seed user '{Username}'.", username);
            return;
        }

        var plaintextApiKey = GenerateApiKey();
        var keyHash = apiKeyHasher.HashApiKey(plaintextApiKey);
        var keyPrefix = plaintextApiKey[..8];
        await apiKeyRepository.CreateAsync(
            user.Id,
            keyHash,
            keyPrefix,
            apiKeyName,
            string.Empty,
            cancellationToken);

        if (hostEnvironment.IsDevelopment())
        {
            logger.LogInformation(
                "Seeded user '{Username}'. Dev API key (save now, not stored): {ApiKeyPrefix}...",
                username,
                keyPrefix);
            logger.LogWarning("Dev-only API key plaintext: {PlaintextApiKey}", plaintextApiKey);
        }
        else
        {
            logger.LogInformation("Seeded user '{Username}' with API key prefix {ApiKeyPrefix}", username, keyPrefix);
        }
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"ih-{Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }
}
