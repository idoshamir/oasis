using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Implementations;

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var idText = id.ToString("D").ToLowerInvariant();
        return dbContext.Users
            .FromSqlInterpolated($"""
                SELECT "Id", "PasswordHash", "Salt", "Username"
                FROM "Users"
                WHERE lower("Id") = {idText}
                LIMIT 1
                """)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking().AnyAsync(u => u.Username == username, cancellationToken);

    public async Task<User> CreateAsync(
        string username,
        string passwordHash,
        string salt,
        CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = passwordHash,
            Salt = salt
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }
}
