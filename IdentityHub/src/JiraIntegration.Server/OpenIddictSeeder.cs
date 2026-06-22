namespace JiraIntegration.Server;

public sealed class OpenIddictSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<OpenIddictSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<OpenIddictClientBootstrap>();
        await bootstrap.EnsureRegisteredAsync(cancellationToken);
        logger.LogDebug("OpenIddict client bootstrap completed.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
