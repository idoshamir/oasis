using System.Security.Claims;
using JiraIntegration.Server.Interfaces;

namespace JiraIntegration.Server.Implementations;

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid? GetUserId()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }

        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue("userId");
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    public string? GetScopedProjectKey() =>
        httpContextAccessor.HttpContext?.User.FindFirstValue("projectKey");
}
