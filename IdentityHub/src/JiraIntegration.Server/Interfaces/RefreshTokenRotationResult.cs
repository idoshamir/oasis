using JiraIntegration.Server.Data.Entities;

namespace JiraIntegration.Server.Interfaces;

public sealed record RefreshTokenRotationResult(User User, string RefreshToken);
