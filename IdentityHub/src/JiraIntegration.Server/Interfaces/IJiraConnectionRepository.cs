using JiraIntegration.Server.Data.Entities;

namespace JiraIntegration.Server.Interfaces;

public interface IJiraConnectionRepository
{
    Task<JiraConnection?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveAsync(JiraConnection connection, CancellationToken cancellationToken = default);
    Task<bool> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
