using JiraIntegration.Server.Data.Entities;

namespace JiraIntegration.Server.Interfaces;

public interface IJiraConnectionValidator
{
    Task<JiraConnection?> GetUsableAsync(Guid userId, CancellationToken cancellationToken = default);
}
