using System.Security.Cryptography;
using System.Text;

namespace JiraIntegration.Server.Auth;

public static class LegacyPasswordVerifier
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

    public static bool HasLegacyCredentials(string? legacySalt, string? legacyPasswordHash) =>
        !string.IsNullOrWhiteSpace(legacySalt) && !string.IsNullOrWhiteSpace(legacyPasswordHash);

    public static bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHash = Convert.FromBase64String(hash);
        var actualHash = DeriveKey(password, saltBytes);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    public static void RunConstantTimeVerification(string password) =>
        Verify(password, DummyHash, DummySalt);

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
}
