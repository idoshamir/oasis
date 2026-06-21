using System.Security.Cryptography;
using System.Text;
using JiraIntegration.Server.Interfaces;

namespace JiraIntegration.Server.Implementations;

public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    private static readonly string DummySalt = Convert.ToBase64String(new byte[SaltSize]);
    private static readonly string DummyHash = Convert.ToBase64String(
        Rfc2898DeriveBytes.Pbkdf2(
            "dummy-verification",
            Convert.FromBase64String(DummySalt),
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize));

    public (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = DeriveKey(password, saltBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHash = Convert.FromBase64String(hash);
        var actualHash = DeriveKey(password, saltBytes);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    public string HashApiKey(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes);
    }

    public void RunConstantTimeVerification(string password) =>
        VerifyPassword(password, DummyHash, DummySalt);

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
}
