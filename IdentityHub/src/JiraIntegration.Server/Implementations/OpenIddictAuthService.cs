using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace JiraIntegration.Server.Implementations;

public sealed class OpenIddictAuthService(
    IHttpClientFactory httpClientFactory,
    IServer server,
    IOptions<JwtOptions> jwtOptions,
    IOptions<OpenIddictClientOptions> clientOptions,
    IOpenIddictTokenManager tokenManager,
    ILogger<OpenIddictAuthService> logger) : IAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly OpenIddictClientOptions _clientOptions = clientOptions.Value;

    public async Task<AuthSessionResult?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var tokenResponse = await RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.Password,
                ["username"] = username,
                ["password"] = password,
                ["client_id"] = _clientOptions.ClientId,
                ["client_secret"] = _clientOptions.ClientSecret
            },
            cancellationToken);

        if (tokenResponse is null)
        {
            logger.LogWarning("Failed login attempt for user {Username}", username);
            return null;
        }

        logger.LogInformation("Successful login for user {Username}", username);
        return tokenResponse;
    }

    public async Task<AuthSessionResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenResponse = await RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.RefreshToken,
                ["refresh_token"] = refreshToken,
                ["client_id"] = _clientOptions.ClientId,
                ["client_secret"] = _clientOptions.ClientSecret
            },
            cancellationToken);

        if (tokenResponse is null)
        {
            return null;
        }

        var username = TryGetUsername(tokenResponse.AccessToken);
        logger.LogInformation("Refreshed session for user {Username}", username ?? "unknown");
        return tokenResponse;
    }

    public async Task LogoutAsync(
        string? accessToken,
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await RevokeByReferenceAsync(refreshToken, cancellationToken);
        }

        var subject = TryGetSubject(accessToken);
        if (subject is not null)
        {
            await foreach (var entry in tokenManager.FindBySubjectAsync(subject, cancellationToken))
            {
                await tokenManager.TryRevokeAsync(entry, cancellationToken);
            }
        }

        var username = TryGetUsername(accessToken);
        logger.LogInformation("User {Username} logged out", username ?? "unknown");
    }

    private async Task RevokeByReferenceAsync(string token, CancellationToken cancellationToken)
    {
        var entry = await tokenManager.FindByReferenceIdAsync(token, cancellationToken);
        if (entry is not null)
        {
            await tokenManager.TryRevokeAsync(entry, cancellationToken);
        }
    }

    private static string? TryGetSubject(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == OpenIddictConstants.Claims.Subject)?.Value;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private async Task<AuthSessionResult?> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("OpenIddictInternal");
        var baseAddress = GetServerBaseAddress();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseAddress}/connect/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TokenResponseDto>(stream, JsonOptions, cancellationToken);
        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.AccessToken) ||
            string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            return null;
        }

        var expiresAt = TryGetExpiresAt(payload.AccessToken)
            ?? DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        return new AuthSessionResult(payload.AccessToken, expiresAt, payload.RefreshToken);
    }

    private string GetServerBaseAddress()
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            ?? addresses?.FirstOrDefault()
            ?? "http://localhost:5000";
        return address.TrimEnd('/');
    }

    private static DateTimeOffset? TryGetExpiresAt(string accessToken)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return jwt.ValidTo;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? TryGetUsername(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == OpenIddictConstants.Claims.Name)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == OpenIddictConstants.Claims.PreferredUsername)?.Value;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed record TokenResponseDto(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
