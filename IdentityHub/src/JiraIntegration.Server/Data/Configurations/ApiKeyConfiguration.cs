using JiraIntegration.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraIntegration.Server.Data.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.KeyHash).HasMaxLength(512).IsRequired();
        builder.Property(k => k.KeyPrefix).HasMaxLength(16);
        builder.Property(k => k.Name).HasMaxLength(256).IsRequired();
        builder.Property(k => k.ProjectKey).HasMaxLength(32).IsRequired();
        builder.HasIndex(k => k.KeyHash);
        builder.HasOne(k => k.User)
            .WithMany(u => u.ApiKeys)
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
