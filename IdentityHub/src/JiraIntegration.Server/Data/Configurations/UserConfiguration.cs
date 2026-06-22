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
        builder.HasIndex(u => u.NormalizedUserName).IsUnique();
    }
}
