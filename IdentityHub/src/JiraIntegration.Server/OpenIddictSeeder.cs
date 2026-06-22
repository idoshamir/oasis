using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Data;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace JiraIntegration.Server;

public sealed class OpenIddictSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<OpenIddictClientOptions> clientOptions,
    ILogger<OpenIddictSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var options = clientOptions.Value;

        if (await applicationManager.FindByClientIdAsync(options.ClientId, cancellationToken) is not null)
        {
            return;
        }

        await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                Permissions.Prefixes.GrantType + GrantTypes.Password,
                Permissions.Prefixes.GrantType + GrantTypes.RefreshToken
            }
        }, cancellationToken);

        logger.LogInformation("Registered OpenIddict client application {ClientId}.", options.ClientId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
