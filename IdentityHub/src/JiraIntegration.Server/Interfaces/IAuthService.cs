namespace JiraIntegration.Server.Interfaces;

public interface IAuthService
{
    Task<AuthSessionResult?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<AuthSessionResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? accessToken, string? refreshToken, CancellationToken cancellationToken = default);
}
