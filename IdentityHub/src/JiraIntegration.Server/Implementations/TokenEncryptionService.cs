using JiraIntegration.Server.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace JiraIntegration.Server.Implementations;

public sealed class TokenEncryptionService(IDataProtectionProvider dataProtectionProvider) : ITokenEncryptionService
{
    private const string ProtectorPurpose = "JiraIntegration.OAuthTokens.v1";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

    public string Encrypt(string plaintext) => _protector.Protect(plaintext);

    public string Decrypt(string ciphertext) => _protector.Unprotect(ciphertext);
}
