using JiraIntegration.Server.Models.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JiraIntegration.Server.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var (statusCode, title, detail, code) = MapException(exception);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = code;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Title, string Detail, string Code) MapException(Exception exception) =>
        exception switch
        {
            JiraNotConnectedException jiraNotConnected => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                jiraNotConnected.Message,
                "jira_not_connected"),
            AtlassianPermissionException atlassianPermission => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                atlassianPermission.Message,
                "atlassian_permission_denied"),
            ArgumentException argument => (StatusCodes.Status400BadRequest, "Bad Request", argument.Message, "validation_error"),
            InvalidOperationException operation => (StatusCodes.Status400BadRequest, "Bad Request", operation.Message, "operational_error"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.", "internal_error")
        };
}
