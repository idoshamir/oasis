namespace JiraIntegration.Server.Models.Exceptions;

public sealed class AtlassianPermissionException : Exception
{
    public AtlassianPermissionException()
        : base("The user associated with this API key does not have permission to write to the specified Jira project.")
    {
    }
}
