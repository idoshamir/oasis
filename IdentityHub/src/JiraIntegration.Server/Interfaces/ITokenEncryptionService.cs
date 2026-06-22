namespace JiraIntegration.Server.Interfaces;

public interface ITokenEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
