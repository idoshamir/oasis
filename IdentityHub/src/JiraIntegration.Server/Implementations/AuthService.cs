using System.IdentityModel.Tokens.Jwt;
using JiraIntegration.Server.Interfaces;
using Microsoft.Extensions.Logging;

namespace JiraIntegration.Server.Implementations;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ITokenRevocationService tokenRevocationService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthTokenResult?> LoginAsync(
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
        return jwtTokenService.CreateToken(user);
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        await tokenRevocationService.RevokeAsync(accessToken, cancellationToken);

        var username = TryGetUsername(accessToken);
        logger.LogInformation("User {Username} logged out", username ?? "unknown");
    }

    private static string? TryGetUsername(string accessToken)
    {
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
