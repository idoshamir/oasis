using JiraIntegration.Server.Auth;
using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Common;
using JiraIntegration.Server.Models.Exceptions;
using JiraIntegration.Server.Models.Nhi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JiraIntegration.Server.Controllers;

[ApiController]
[Route("api/v1/nhi-findings")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.AuthenticationScheme)]
[EnableRateLimiting("ExternalApi")]
public sealed class NhiFindingsController(
    ICurrentUserAccessor currentUserAccessor,
    ITicketCreationPipeline ticketCreationPipeline) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(NhiFindingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateFinding(
        [FromBody] CreateNhiFindingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var scopedProjectKey = currentUserAccessor.GetScopedProjectKey();
        if (!string.IsNullOrWhiteSpace(scopedProjectKey)
            && !string.IsNullOrWhiteSpace(request.ProjectKey)
            && !string.Equals(scopedProjectKey, request.ProjectKey, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponse(
                "ProjectKey does not match the API key scope.",
                "validation_error"));
        }

        var effectiveProjectKey = !string.IsNullOrWhiteSpace(scopedProjectKey)
            ? scopedProjectKey
            : request.ProjectKey;

        try
        {
            var result = await ticketCreationPipeline.CreateTicketAsync(
                userId.Value,
                request.Title,
                request.Description,
                effectiveProjectKey,
                cancellationToken);

            return Created(
                result.IssueBrowseUrl,
                new NhiFindingResponse(result.IssueKey, result.IssueId, result.LedgerId));
        }
        catch (JiraNotConnectedException)
        {
            return BadRequest(new ErrorResponse(
                "User has not connected a Jira workspace.",
                "jira_not_connected"));
        }
        catch (AtlassianPermissionException)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new ErrorResponse(
                    "The user associated with this API key does not have permission to write to the specified Jira project.",
                    "atlassian_permission_denied"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message, "validation_error"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message, "jira_error"));
        }
    }
}
