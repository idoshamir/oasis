using JiraIntegration.Server.Configuration;
using Microsoft.AspNetCore.Http;

namespace JiraIntegration.Server.Auth;

public static class RefreshTokenCookie
{
    public const string Name = "refresh_token";
    public const string Path = "/api/auth";

    public static void Set(HttpResponse response, string refreshToken, JwtOptions options, IHostEnvironment environment)
    {
        response.Cookies.Append(Name, refreshToken, BuildOptions(options, environment));
    }

    public static void Clear(HttpResponse response, IHostEnvironment environment)
    {
        response.Cookies.Delete(Name, new CookieOptions
        {
            Path = Path,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax
        });
    }

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static CookieOptions BuildOptions(JwtOptions options, IHostEnvironment environment) =>
        new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = Path,
            Expires = DateTimeOffset.UtcNow.AddDays(options.RefreshTokenExpiryDays)
        };
}
