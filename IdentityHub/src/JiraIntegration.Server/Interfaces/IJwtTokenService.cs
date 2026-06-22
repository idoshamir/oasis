using JiraIntegration.Server.Data.Entities;

namespace JiraIntegration.Server.Interfaces;

public interface IJwtTokenService
{
    AuthTokenResult CreateToken(User user);
}
