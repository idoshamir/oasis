using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityModel.Client;
using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Jira;
using Microsoft.Extensions.Options;

namespace JiraIntegration.Server.Implementations;

public sealed class JiraOAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<AtlassianOptions> atlassianOptions) : IJiraOAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AtlassianOptions _options = atlassianOptions.Value;

    public string BuildAuthorizationUrl(string state)
    {
        var query = new Dictionary<string, string>
        {
            ["audience"] = "api.atlassian.com",
            ["client_id"] = _options.ClientId,
            ["scope"] = _options.Scopes,
            ["redirect_uri"] = _options.RedirectUri,
            ["state"] = state,
            ["response_type"] = "code",
            ["prompt"] = "consent"
        };

        var queryString = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"https://auth.atlassian.com/authorize?{queryString}";
    }

    public async Task<AtlassianTokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("AtlassianOAuth");
        var response = await client.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = "https://auth.atlassian.com/oauth/token",
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            Code = code,
            RedirectUri = _options.RedirectUri
        }, cancellationToken);

        if (response.IsError)
        {
            throw new InvalidOperationException(
                $"Atlassian token exchange failed: {response.Error} {response.ErrorDescription}");
        }

        if (string.IsNullOrWhiteSpace(response.AccessToken))
        {
            throw new InvalidOperationException("Atlassian token exchange returned an empty response.");
        }

        return new AtlassianTokenResponse(
            response.AccessToken,
            response.RefreshToken ?? string.Empty,
            response.ExpiresIn);
    }

    public async Task<AtlassianTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("Jira connection expired. Please reconnect Jira in the dashboard.");
        }

        var client = httpClientFactory.CreateClient("AtlassianOAuth");
        var response = await client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = "https://auth.atlassian.com/oauth/token",
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            RefreshToken = refreshToken
        }, cancellationToken);

        if (response.IsError || string.IsNullOrWhiteSpace(response.AccessToken))
        {
            throw new InvalidOperationException("Jira connection expired. Please reconnect Jira in the dashboard.");
        }

        return new AtlassianTokenResponse(
            response.AccessToken,
            response.RefreshToken ?? refreshToken,
            response.ExpiresIn);
    }

    public async Task<AtlassianAccessibleResource> GetPrimaryAccessibleResourceAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Atlassian");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.atlassian.com/oauth/token/accessible-resources");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var resources = await JsonSerializer.DeserializeAsync<List<AtlassianAccessibleResource>>(stream, JsonOptions, cancellationToken);
        var resource = resources?.FirstOrDefault();
        if (resource is null || string.IsNullOrWhiteSpace(resource.Id))
        {
            throw new InvalidOperationException("No accessible Atlassian resources were returned.");
        }

        return resource;
    }
}
