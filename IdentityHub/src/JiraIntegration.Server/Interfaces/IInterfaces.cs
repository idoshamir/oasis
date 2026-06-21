using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Models.Jira;
using JiraIntegration.Server.Models.Tickets;

namespace JiraIntegration.Server.Interfaces;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);
    bool VerifyPassword(string password, string hash, string salt);
    string HashApiKey(string apiKey);
    void RunConstantTimeVerification(string password);
}

public interface ITokenEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}

public interface IJwtTokenService
{
    AuthTokenResult CreateToken(User user);
}

public sealed record AuthTokenResult(string Token, DateTimeOffset ExpiresAt);

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<User> CreateAsync(string username, string passwordHash, string salt, CancellationToken cancellationToken = default);
}

public interface IJiraConnectionRepository
{
    Task<JiraConnection?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveAsync(JiraConnection connection, CancellationToken cancellationToken = default);
}

public interface IApiKeyRepository
{
    Task<ApiKey?> GetActiveByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiKey> CreateAsync(
        Guid userId,
        string keyHash,
        string? keyPrefix,
        string name,
        string projectKey,
        CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid userId, Guid keyId, CancellationToken cancellationToken = default);
    Task<ApiKey?> GetActiveByUserIdAndProjectKeyAsync(
        Guid userId,
        string projectKey,
        CancellationToken cancellationToken = default);
}

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyListItem>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<GeneratedApiKeyResult> GenerateAsync(
        Guid userId,
        string name,
        string projectKey,
        CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid userId, Guid keyId, CancellationToken cancellationToken = default);
    Task<GeneratedApiKeyResult> RegenerateAsync(
        Guid userId,
        string projectKey,
        CancellationToken cancellationToken = default);
}

public sealed record ApiKeyListItem(
    Guid Id,
    string Name,
    string ProjectKey,
    string MaskedKey,
    DateTimeOffset CreatedAt);

public sealed record GeneratedApiKeyResult(
    Guid Id,
    string Name,
    string ProjectKey,
    string PlaintextKey,
    DateTimeOffset CreatedAt);

public interface INhiTicketLedgerRepository
{
    Task<NhiTicketLedger> AddAsync(NhiTicketLedger entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NhiTicketLedger>> GetRecentByUserIdAsync(
        Guid userId,
        int count,
        string? projectKey = null,
        CancellationToken cancellationToken = default);
}

public interface IJiraOAuthService
{
    string BuildAuthorizationUrl(string state);
    Task<AtlassianTokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<AtlassianTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
    Task<AtlassianAccessibleResource> GetPrimaryAccessibleResourceAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}

public interface IOAuthStateStore
{
    string CreateState(Guid userId);
    Guid? ValidateAndConsume(string state);
}

public interface IJiraTicketService
{
    Task<JiraProjectTarget> ResolveProjectTargetAsync(
        string cloudId,
        string accessToken,
        string? preferredProjectKey = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraProjectOption>> GetCreatableProjectsAsync(
        string cloudId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<CreatedJiraIssue> CreateTaskAsync(
        string cloudId,
        string accessToken,
        string projectKey,
        string issueTypeName,
        string title,
        string description,
        CancellationToken cancellationToken = default);
}

public interface IAuthService
{
    Task<AuthTokenResult?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default);
}

public interface ITokenRevocationService
{
    Task RevokeAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<bool> IsRevokedAsync(string accessToken, CancellationToken cancellationToken = default);
}

public interface IRevokedTokenRepository
{
    Task<bool> IsRevokedAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task RevokeAsync(string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task DeleteExpiredAsync(CancellationToken cancellationToken = default);
}

public interface ITicketCreationPipeline
{
    Task<JiraProjectsResponse?> GetCreatableProjectsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<TicketCreationResult> CreateTicketAsync(
        Guid userId,
        string title,
        string description,
        string? projectKey = null,
        CancellationToken cancellationToken = default);

    Task<RecentTicketsResponse?> GetRecentTicketsAsync(
        Guid userId,
        int count = 10,
        string? projectKey = null,
        CancellationToken cancellationToken = default);
}

public sealed record TicketPipelineError(string Message, string Code);

public interface IJiraOAuthPipeline
{
    Task<string> CompleteOAuthAsync(string code, string state, CancellationToken cancellationToken = default);
}
