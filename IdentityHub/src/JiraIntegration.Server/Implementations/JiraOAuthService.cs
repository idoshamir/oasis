using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Models.Jira;
using JiraIntegration.Server.Interfaces;
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
        var client = httpClientFactory.CreateClient("Atlassian");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri
        });

        using var response = await client.PostAsync("https://auth.atlassian.com/oauth/token", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var tokenResponse = await JsonSerializer.DeserializeAsync<AtlassianTokenResponseDto>(stream, JsonOptions, cancellationToken);
        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Atlassian token exchange returned an empty response.");
        }

        return new AtlassianTokenResponse(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken ?? string.Empty,
            tokenResponse.ExpiresIn);
    }

    public async Task<AtlassianTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("Jira connection expired. Please reconnect Jira in the dashboard.");
        }

        var client = httpClientFactory.CreateClient("Atlassian");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken
        });

        using var response = await client.PostAsync("https://auth.atlassian.com/oauth/token", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Jira connection expired. Please reconnect Jira in the dashboard.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var tokenResponse = await JsonSerializer.DeserializeAsync<AtlassianTokenResponseDto>(stream, JsonOptions, cancellationToken);
        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Jira connection expired. Please reconnect Jira in the dashboard.");
        }

        return new AtlassianTokenResponse(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken ?? refreshToken,
            tokenResponse.ExpiresIn);
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

    private sealed record AtlassianTokenResponseDto(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
