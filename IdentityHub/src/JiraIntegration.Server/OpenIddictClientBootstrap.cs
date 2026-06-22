using JiraIntegration.Server.Configuration;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace JiraIntegration.Server;

public sealed class OpenIddictClientBootstrap(
    IOpenIddictApplicationManager applicationManager,
    IOptions<OpenIddictClientOptions> clientOptions,
    ILogger<OpenIddictClientBootstrap> logger)
{
    public async Task EnsureRegisteredAsync(CancellationToken cancellationToken = default)
    {
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
}
