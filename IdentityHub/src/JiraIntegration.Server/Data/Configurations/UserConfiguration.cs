using JiraIntegration.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraIntegration.Server.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.Property(u => u.UserName).HasMaxLength(128).IsRequired();
        builder.Property(u => u.LegacySalt).HasMaxLength(128);
        builder.Property(u => u.LegacyPasswordHash).HasMaxLength(512);
        builder.HasIndex(u => u.NormalizedUserName).IsUnique();
    }
}
