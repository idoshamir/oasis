using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JiraIntegration.Server.Auth;
using JiraIntegration.Server.Data.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace JiraIntegration.Server.Controllers;

public sealed class ConnectController(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IOpenIddictApplicationManager applicationManager) : Controller
{
    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordGrantAsync(request, cancellationToken);
        }

        if (request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var user = await userManager.GetUserAsync(result.Principal!);
            if (user is null)
            {
                return InvalidGrant("The refresh token is no longer valid.");
            }

            var principal = await CreatePrincipalAsync(user, cancellationToken);
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    private async Task<IActionResult> HandlePasswordGrantAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        if (!await ValidateClientAsync(request, cancellationToken))
        {
            return Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidClient
                }));
        }

        var username = request.Username;
        var password = request.Password;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            LegacyPasswordVerifier.RunConstantTimeVerification(password ?? string.Empty);
            return InvalidGrant("Invalid username or password.");
        }

        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            LegacyPasswordVerifier.RunConstantTimeVerification(password);
            return InvalidGrant("Invalid username or password.");
        }

        if (!await ValidatePasswordAsync(user, password))
        {
            return InvalidGrant("Invalid username or password.");
        }

        var principal = await CreatePrincipalAsync(user, cancellationToken);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<bool> ValidateClientAsync(OpenIddictRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return false;
        }

        var application = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
        if (application is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return false;
        }

        return await applicationManager.ValidateClientSecretAsync(application, request.ClientSecret, cancellationToken);
    }

    private async Task<bool> ValidatePasswordAsync(User user, string password)
    {
        if (LegacyPasswordVerifier.HasLegacyCredentials(user.LegacySalt, user.LegacyPasswordHash))
        {
            if (!LegacyPasswordVerifier.Verify(password, user.LegacyPasswordHash!, user.LegacySalt!))
            {
                return false;
            }

            var resetResult = await userManager.RemovePasswordAsync(user);
            if (!resetResult.Succeeded)
            {
                return false;
            }

            var addResult = await userManager.AddPasswordAsync(user, password);
            if (!addResult.Succeeded)
            {
                return false;
            }

            user.LegacySalt = null;
            user.LegacyPasswordHash = null;
            await userManager.UpdateAsync(user);
            return true;
        }

        return await userManager.CheckPasswordAsync(user, password);
    }

    private async Task<ClaimsPrincipal> CreatePrincipalAsync(User user, CancellationToken cancellationToken)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        principal.SetScopes(Scopes.OpenId, Scopes.OfflineAccess);
        principal.SetDestinations(GetDestinations);

        var identity = (ClaimsIdentity)principal.Identity!;
        var subject = user.Id.ToString("D").ToLowerInvariant();

        if (identity.FindFirst(Claims.Subject) is null)
        {
            identity.AddClaim(new Claim(Claims.Subject, subject));
        }

        if (identity.FindFirst(JwtRegisteredClaimNames.Sub) is null)
        {
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, subject));
        }

        var uniqueNameClaim = identity.FindFirst(JwtRegisteredClaimNames.UniqueName);
        if (uniqueNameClaim is not null)
        {
            identity.RemoveClaim(uniqueNameClaim);
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName));
        }

        return principal;
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        if (claim.Type is Claims.Subject or Claims.Name or Claims.PreferredUsername
            or JwtRegisteredClaimNames.Sub or JwtRegisteredClaimNames.UniqueName)
        {
            yield return Destinations.AccessToken;
            yield return Destinations.IdentityToken;
        }
    }

    private ForbidResult InvalidGrant(string description) =>
        Forbid(
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }));
}
