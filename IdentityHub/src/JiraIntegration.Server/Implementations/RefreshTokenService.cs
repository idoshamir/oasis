using System.Security.Cryptography;
using System.Text;
using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.Extensions.Options;

namespace JiraIntegration.Server.Implementations;

public sealed class RefreshTokenService(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IOptions<JwtOptions> jwtOptions) : IRefreshTokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plaintext = GenerateToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenExpiryDays);
        await refreshTokenRepository.CreateAsync(userId, Hash(plaintext), expiresAt, cancellationToken);
        return plaintext;
    }

    public async Task<RefreshTokenRotationResult?> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenHash = Hash(refreshToken);
        var entry = await refreshTokenRepository.GetValidByHashAsync(tokenHash, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var user = await userRepository.GetByIdAsync(entry.UserId, cancellationToken);
        if (user is null)
        {
            await refreshTokenRepository.RevokeByHashAsync(tokenHash, cancellationToken);
            return null;
        }

        await refreshTokenRepository.RevokeByHashAsync(tokenHash, cancellationToken);
        var newRefreshToken = await IssueAsync(user.Id, cancellationToken);
        return new RefreshTokenRotationResult(user, newRefreshToken);
    }

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Task.CompletedTask;
        }

        return refreshTokenRepository.RevokeByHashAsync(Hash(refreshToken), cancellationToken);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"rt-{Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }

    private static string Hash(string refreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(hash);
    }
}
