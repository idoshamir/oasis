namespace JiraIntegration.Server.Models.Exceptions;

public sealed class JiraNotConnectedException : Exception
{
    public JiraNotConnectedException()
        : base("User has not connected a Jira workspace.")
    {
    }

    public JiraNotConnectedException(string message)
        : base(message)
    {
    }
}
