using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.Common;
using JiraIntegration.Server.Models.Exceptions;
using JiraIntegration.Server.Models.Jira;
using JiraIntegration.Server.Models.Tickets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JiraIntegration.Server.Controllers;

[ApiController]
[Route("api/ui/tickets")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class UiTicketsController(
    ICurrentUserAccessor currentUserAccessor,
    ITicketCreationPipeline ticketCreationPipeline) : ControllerBase
{
    [HttpGet("projects")]
    [ProducesResponseType(typeof(JiraProjectsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await ticketCreationPipeline.GetCreatableProjectsAsync(userId.Value, cancellationToken);
            if (result is null)
            {
                return Ok(new JiraProjectsResponse([], null));
            }

            return Ok(result);
        }
        catch (JiraNotConnectedException)
        {
            return Ok(new JiraProjectsResponse([], null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message, "jira_error"));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(UiTicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTicket(
        [FromBody] CreateUiTicketRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await ticketCreationPipeline.CreateTicketAsync(
                userId.Value,
                request.Title,
                request.Description,
                request.ProjectKey,
                cancellationToken);

            return Created(
                string.Empty,
                new UiTicketResponse(result.IssueKey, result.IssueId, result.Title, result.CreatedAt));
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message, "jira_error"));
        }
    }

    [HttpGet("recent")]
    [ProducesResponseType(typeof(RecentTicketsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecentTickets(
        [FromQuery] string? projectKey,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await ticketCreationPipeline.GetRecentTicketsAsync(
                userId.Value,
                projectKey: projectKey,
                cancellationToken: cancellationToken);
            if (result is null)
            {
                return Ok(new RecentTicketsResponse([]));
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message, "jira_error"));
        }
    }
}
