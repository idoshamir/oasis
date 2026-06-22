using JiraIntegration.Server.Data.Entities;

namespace JiraIntegration.Server.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<User> CreateAsync(string username, string passwordHash, string salt, CancellationToken cancellationToken = default);
}
