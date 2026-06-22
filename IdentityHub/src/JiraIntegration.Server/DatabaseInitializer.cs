using JiraIntegration.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server;

public sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync(cancellationToken);

        logger.LogInformation("Database initialized.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
