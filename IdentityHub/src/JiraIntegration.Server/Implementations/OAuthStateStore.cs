using System.Text;
using JiraIntegration.Server.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace JiraIntegration.Server.Implementations;

public sealed class OAuthStateStore(IDataProtectionProvider dataProtectionProvider) : IOAuthStateStore
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("JiraIntegration.Server.OAuthState");

    public string CreateState(Guid userId)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(StateLifetime).ToUnixTimeSeconds();
        var payload = $"{userId:N}|{expiresAt}";
        var protectedBytes = _protector.Protect(Utf8.GetBytes(payload));
        return WebEncoders.Base64UrlEncode(protectedBytes);
    }

    public Guid? ValidateAndConsume(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        byte[] protectedBytes;
        try
        {
            protectedBytes = WebEncoders.Base64UrlDecode(state);
        }
        catch (FormatException)
        {
            return null;
        }

        byte[] payloadBytes;
        try
        {
            payloadBytes = _protector.Unprotect(protectedBytes);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }

        var payload = Utf8.GetString(payloadBytes);
        var separatorIndex = payload.IndexOf('|');
        if (separatorIndex <= 0 || separatorIndex >= payload.Length - 1)
        {
            return null;
        }

        var userIdSegment = payload[..separatorIndex];
        var expirySegment = payload[(separatorIndex + 1)..];
        if (!Guid.TryParseExact(userIdSegment, "N", out var userId)
            || !long.TryParse(expirySegment, out var expiresAtUnix))
        {
            return null;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnix)
        {
            return null;
        }

        return userId;
    }
}
