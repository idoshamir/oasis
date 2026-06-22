using System.Globalization;
using System.Security.Cryptography;
using JiraIntegration.Server.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace JiraIntegration.Server.Implementations;

public sealed class OAuthStateProtector(IDataProtectionProvider dataProtectionProvider) : IOAuthStateStore
{
    private const string ProtectorPurpose = "JiraIntegration.OAuthState.v1";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

    public string CreateState(Guid userId)
    {
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        var expiresAt = DateTimeOffset.UtcNow.Add(StateLifetime).ToUnixTimeSeconds();
        return _protector.Protect($"{userId:D}|{expiresAt.ToString(CultureInfo.InvariantCulture)}|{nonce}");
    }

    public Guid? ValidateAndConsume(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(state);
            var parts = payload.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                return null;
            }

            if (!Guid.TryParse(parts[0], out var userId))
            {
                return null;
            }

            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresAtUnix))
            {
                return null;
            }

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix);
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                return null;
            }

            return userId;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
