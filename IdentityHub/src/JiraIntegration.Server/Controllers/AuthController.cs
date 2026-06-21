using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Auth;
using JiraIntegration.Server.Models.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JiraIntegration.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
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

        return Ok(new AuthResponse(result.Token, result.ExpiresAt));
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!TryGetBearerToken(out var token))
        {
            return Unauthorized();
        }

        await authService.LogoutAsync(token, cancellationToken);
        return NoContent();
    }

    private bool TryGetBearerToken(out string token)
    {
        token = string.Empty;
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

        token = header[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }
}
