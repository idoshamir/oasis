using System.IdentityModel.Tokens.Jwt;
using JiraIntegration.Server.Interfaces;
using Microsoft.Extensions.Logging;

namespace JiraIntegration.Server.Implementations;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    ITokenRevocationService tokenRevocationService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthSessionResult?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            passwordHasher.RunConstantTimeVerification(password);
            logger.LogWarning("Failed login attempt for user {Username}", username);
            return null;
        }

        if (!passwordHasher.VerifyPassword(password, user.PasswordHash, user.Salt))
        {
            logger.LogWarning("Failed login attempt for user {Username}", username);
            return null;
        }

        logger.LogInformation("Successful login for user {Username}", username);
        var accessToken = jwtTokenService.CreateToken(user);
        var refreshToken = await refreshTokenService.IssueAsync(user.Id, cancellationToken);
        return new AuthSessionResult(accessToken.Token, accessToken.ExpiresAt, refreshToken);
    }

    public async Task<AuthSessionResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var rotation = await refreshTokenService.RotateAsync(refreshToken, cancellationToken);
        if (rotation is null)
        {
            return null;
        }

        var accessToken = jwtTokenService.CreateToken(rotation.User);
        logger.LogInformation("Refreshed session for user {Username}", rotation.User.Username);
        return new AuthSessionResult(accessToken.Token, accessToken.ExpiresAt, rotation.RefreshToken);
    }

    public async Task LogoutAsync(
        string? accessToken,
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            await tokenRevocationService.RevokeAsync(accessToken, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await refreshTokenService.RevokeAsync(refreshToken, cancellationToken);
        }

        var username = TryGetUsername(accessToken);
        logger.LogInformation("User {Username} logged out", username ?? "unknown");
    }

    private static string? TryGetUsername(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            return jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
