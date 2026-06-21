using JiraIntegration.Server.Auth;
using JiraIntegration.Server.Configuration;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Auth;
using JiraIntegration.Server.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace JiraIntegration.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IOptions<JwtOptions> jwtOptions,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Username, request.Password, cancellationToken);
        if (result is null)
        {
            return Unauthorized(new ErrorResponse("Invalid username or password.", "invalid_credentials"));
        }

        RefreshTokenCookie.Set(Response, result.RefreshToken, jwtOptions.Value, environment);
        return Ok(new AuthResponse(result.AccessToken, result.ExpiresAt));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = RefreshTokenCookie.Read(Request);
        if (refreshToken is null)
        {
            return Unauthorized(new ErrorResponse("Session expired. Please sign in again.", "session_expired"));
        }

        var result = await authService.RefreshAsync(refreshToken, cancellationToken);
        if (result is null)
        {
            RefreshTokenCookie.Clear(Response, environment);
            return Unauthorized(new ErrorResponse("Session expired. Please sign in again.", "session_expired"));
        }

        RefreshTokenCookie.Set(Response, result.RefreshToken, jwtOptions.Value, environment);
        return Ok(new AuthResponse(result.AccessToken, result.ExpiresAt));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        TryGetBearerToken(out var accessToken);
        var refreshToken = RefreshTokenCookie.Read(Request);

        await authService.LogoutAsync(accessToken, refreshToken, cancellationToken);
        RefreshTokenCookie.Clear(Response, environment);
        return NoContent();
    }

    private bool TryGetBearerToken(out string? token)
    {
        token = null;
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return false;
        }

        var header = headerValues.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = header[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        token = value;
        return true;
    }
}
