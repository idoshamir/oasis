namespace JiraIntegration.Server.Interfaces;

public interface IJiraOAuthPipeline
{
    Task<string> CompleteOAuthAsync(string code, string state, CancellationToken cancellationToken = default);
}
