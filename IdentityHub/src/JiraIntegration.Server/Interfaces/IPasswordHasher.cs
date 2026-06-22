namespace JiraIntegration.Server.Interfaces;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);
    bool VerifyPassword(string password, string hash, string salt);
    string HashApiKey(string apiKey);
    void RunConstantTimeVerification(string password);
}
