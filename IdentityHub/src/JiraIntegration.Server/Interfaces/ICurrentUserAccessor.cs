namespace JiraIntegration.Server.Interfaces;

public interface ICurrentUserAccessor
{
    Guid? GetUserId();
    string? GetScopedProjectKey();
}
