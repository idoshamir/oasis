using System.Security.Cryptography;
using System.Text;
using JiraIntegration.Server.Interfaces;

namespace JiraIntegration.Server.Implementations;

public sealed class ApiKeyHasher : IApiKeyHasher
{
    public string HashApiKey(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes);
    }
}
