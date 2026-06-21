namespace JiraIntegration.Server.Models.Common;

public sealed record ErrorResponse(string Message, string? Code = null);
