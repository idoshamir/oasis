using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Implementations;

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.UserName == username, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().AnyAsync(u => u.UserName == username, cancellationToken);
}
