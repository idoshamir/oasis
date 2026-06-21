using JiraIntegration.Server.Data;
using JiraIntegration.Server.Data.Entities;
using JiraIntegration.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Implementations;

public sealed class JiraConnectionRepository(AppDbContext dbContext) : IJiraConnectionRepository
{
    public Task<JiraConnection?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.JiraConnections.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public async Task SaveAsync(JiraConnection connection, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.JiraConnections
            .FirstOrDefaultAsync(c => c.UserId == connection.UserId, cancellationToken);

        if (existing is null)
        {
            if (connection.Id == Guid.Empty)
            {
                connection.Id = Guid.NewGuid();
            }

            dbContext.JiraConnections.Add(connection);
        }
        else
        {
            existing.AtlassianCloudId = connection.AtlassianCloudId;
            existing.WorkspaceName = connection.WorkspaceName;
            existing.WorkspaceUrl = connection.WorkspaceUrl;
            existing.DefaultProjectKey = connection.DefaultProjectKey;
            existing.DefaultIssueTypeName = connection.DefaultIssueTypeName;
            existing.EncryptedAccessToken = connection.EncryptedAccessToken;
            existing.EncryptedRefreshToken = connection.EncryptedRefreshToken;
            existing.ExpiresAt = connection.ExpiresAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connection = await dbContext.JiraConnections
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (connection is null)
        {
            return false;
        }

        dbContext.JiraConnections.Remove(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
