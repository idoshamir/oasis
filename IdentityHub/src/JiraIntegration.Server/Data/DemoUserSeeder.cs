using JiraIntegration.Server.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace JiraIntegration.Server.Data;

public sealed class DemoUserSeeder(UserManager<User> userManager, ILogger<DemoUserSeeder> logger)
{
    private static readonly (Guid Id, string Username, string Password)[] DemoUsers =
    [
        (new Guid("a0000000-0000-4000-8000-000000000001"), "demo", "Demo123!"),
        (new Guid("a0000000-0000-4000-8000-000000000002"), "testuser", "Test123!")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (id, username, password) in DemoUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await userManager.FindByNameAsync(username) is not null)
            {
                continue;
            }

            var user = new User
            {
                Id = id,
                UserName = username,
                NormalizedUserName = userManager.NormalizeName(username)
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error => error.Description));
                logger.LogError("Failed to seed demo user '{Username}': {Errors}", username, errors);
                throw new InvalidOperationException($"Failed to seed demo user '{username}'.");
            }

            logger.LogInformation("Seeded demo user '{Username}'.", username);
        }
    }
}
