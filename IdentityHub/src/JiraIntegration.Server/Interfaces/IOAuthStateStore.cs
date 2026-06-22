namespace JiraIntegration.Server.Interfaces;

public interface IOAuthStateStore
{
    string CreateState(Guid userId);
    Guid? ValidateAndConsume(string state);
}
