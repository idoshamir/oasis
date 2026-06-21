using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using JiraIntegration.Server.Interfaces;

namespace JiraIntegration.Server.Implementations;

public sealed class TokenRevocationService(IRevokedTokenRepository revokedTokenRepository) : ITokenRevocationService
{
    public async Task RevokeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var expiresAt = TryGetExpiresAt(accessToken);
        if (expiresAt is null || expiresAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        await revokedTokenRepository.RevokeAsync(
            GetTokenHash(accessToken),
            expiresAt.Value,
            cancellationToken);
    }

    public Task<bool> IsRevokedAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(false);
        }

        return revokedTokenRepository.IsRevokedAsync(GetTokenHash(accessToken), cancellationToken);
    }

    private static string GetTokenHash(string accessToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(accessToken));
        return Convert.ToHexString(hash);
    }

    private static DateTimeOffset? TryGetExpiresAt(string accessToken)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return jwt.ValidTo;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
