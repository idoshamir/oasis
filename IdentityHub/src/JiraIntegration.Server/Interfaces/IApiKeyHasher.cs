namespace JiraIntegration.Server.Interfaces;

public interface IApiKeyHasher
{
    string HashApiKey(string apiKey);
}
