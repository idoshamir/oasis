using JiraIntegration.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraIntegration.Server.Data.Configurations;

public sealed class JiraConnectionConfiguration : IEntityTypeConfiguration<JiraConnection>
{
    public void Configure(EntityTypeBuilder<JiraConnection> builder)
    {
        builder.ToTable("JiraConnections");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.AtlassianCloudId).HasMaxLength(128).IsRequired();
        builder.Property(c => c.WorkspaceName).HasMaxLength(256).IsRequired();
        builder.Property(c => c.WorkspaceUrl).HasMaxLength(512).IsRequired();
        builder.Property(c => c.DefaultProjectKey).HasMaxLength(32).IsRequired();
        builder.Property(c => c.DefaultIssueTypeName).HasMaxLength(64).IsRequired();
        builder.Property(c => c.EncryptedAccessToken).IsRequired();
        builder.Property(c => c.EncryptedRefreshToken).IsRequired();
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.HasOne(c => c.User)
            .WithOne(u => u.JiraConnection)
            .HasForeignKey<JiraConnection>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
