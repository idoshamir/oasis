using JiraIntegration.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraIntegration.Server.Data.Configurations;

public sealed class NhiTicketLedgerConfiguration : IEntityTypeConfiguration<NhiTicketLedger>
{
    public void Configure(EntityTypeBuilder<NhiTicketLedger> builder)
    {
        builder.ToTable("NhiTicketLedgers");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.JiraIssueId).HasMaxLength(64).IsRequired();
        builder.Property(l => l.JiraIssueKey).HasMaxLength(32).IsRequired();
        builder.Property(l => l.Title).HasMaxLength(512).IsRequired();
        builder.HasIndex(l => new { l.UserId, l.CreatedAt });
        builder.HasOne(l => l.User)
            .WithMany(u => u.NhiTicketLedgers)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
