using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Common;
using JiraIntegration.Server.Models.Jira;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using JiraIntegration.Server.Configuration;

namespace JiraIntegration.Server.Controllers;

[ApiController]
[Route("api/jira")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class JiraAuthController(
    ICurrentUserAccessor currentUserAccessor,
    IOAuthStateStore oauthStateStore,
    IUserRepository userRepository,
    IJiraOAuthService jiraOAuthService,
    IJiraOAuthPipeline jiraOAuthPipeline,
    IJiraConnectionValidator jiraConnectionValidator,
    IOptions<AtlassianOptions> atlassianOptions,
    ILogger<JiraAuthController> logger) : ControllerBase
{
    private readonly AtlassianOptions _atlassianOptions = atlassianOptions.Value;

    [HttpGet("connection")]
    [ProducesResponseType(typeof(JiraConnectionStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConnection(CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var connection = await jiraConnectionValidator.GetUsableAsync(userId.Value, cancellationToken);
        if (connection is null)
        {
            return Ok(new JiraConnectionStatusResponse(false, null, null));
        }

        return Ok(new JiraConnectionStatusResponse(
            true,
            connection.WorkspaceName,
            connection.WorkspaceUrl));
    }

    [HttpGet("auth-url")]
    [ProducesResponseType(typeof(JiraAuthUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAuthUrl(CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("OAuth authorization URL request rejected: user not authenticated");
            return Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            logger.LogWarning(
                "OAuth authorization URL request rejected: user {UserId} from token no longer exists",
                userId.Value);
            return Unauthorized();
        }

        if (!_atlassianOptions.IsConfigured())
        {
            logger.LogWarning("OAuth authorization URL request rejected: Atlassian OAuth is not configured");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse(
                    "Atlassian OAuth is not configured. Set Atlassian:ClientId and Atlassian:ClientSecret via dotnet user-secrets (see README.md), or set Atlassian__ClientId and Atlassian__ClientSecret environment variables.",
                    "atlassian_not_configured"));
        }

        logger.LogInformation("OAuth authorization URL requested for user {UserId}", userId.Value);
        var state = oauthStateStore.CreateState(userId.Value);
        var url = jiraOAuthService.BuildAuthorizationUrl(state);
        return Ok(new JiraAuthUrlResponse(url));
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("OAuth callback initiated");

        try
        {
            var redirectUrl = await jiraOAuthPipeline.CompleteOAuthAsync(code, state, cancellationToken);
            logger.LogInformation("OAuth callback completed successfully");
            return Redirect(redirectUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "OAuth callback failed: {FailureReason}", ex.Message);
            return Redirect(BuildFailureRedirectUrl());
        }
    }

    private string BuildFailureRedirectUrl()
    {
        var baseUrl = _atlassianOptions.FrontendSuccessUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "/";
        }

        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}jira_connected=false";
    }
}
