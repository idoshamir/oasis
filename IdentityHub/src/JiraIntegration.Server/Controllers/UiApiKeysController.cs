using JiraIntegration.Server.Interfaces;
using JiraIntegration.Server.Models.ApiKeys;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JiraIntegration.Server.Controllers;

[ApiController]
[Route("api/ui/api-keys")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class UiApiKeysController(
    ICurrentUserAccessor currentUserAccessor,
    IApiKeyService apiKeyService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiKeyListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListKeys(CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var keys = await apiKeyService.ListAsync(userId.Value, cancellationToken);
        var response = keys
            .Select(k => new ApiKeyListItemResponse(k.Id, k.Name, k.ProjectKey, k.MaskedKey, k.CreatedAt))
            .ToList();

        return Ok(new ApiKeyListResponse(response));
    }

    [HttpPost]
    [ProducesResponseType(typeof(GenerateApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateKey(
        [FromBody] GenerateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await apiKeyService.GenerateAsync(
            userId.Value,
            request.Name,
            request.ProjectKey,
            cancellationToken);

        return Created(
            string.Empty,
            new GenerateApiKeyResponse(
                result.Id,
                result.Name,
                result.ProjectKey,
                result.PlaintextKey,
                result.CreatedAt));
    }

    [HttpPost("regenerate")]
    [ProducesResponseType(typeof(GenerateApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegenerateKey(
        [FromBody] RegenerateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await apiKeyService.RegenerateAsync(
            userId.Value,
            request.ProjectKey,
            cancellationToken);

        return Created(
            string.Empty,
            new GenerateApiKeyResponse(
                result.Id,
                result.Name,
                result.ProjectKey,
                result.PlaintextKey,
                result.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeKey(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var revoked = await apiKeyService.RevokeAsync(userId.Value, id, cancellationToken);
        if (!revoked)
        {
            return NotFound();
        }

        return NoContent();
    }
}
