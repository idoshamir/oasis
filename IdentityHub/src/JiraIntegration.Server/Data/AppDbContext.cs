using JiraIntegration.Server.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JiraIntegration.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    private static readonly ValueConverter<Guid, string> LowercaseGuidConverter = new(
        guid => guid.ToString("D").ToLowerInvariant(),
        text => Guid.Parse(text));

    public DbSet<JiraConnection> JiraConnections => Set<JiraConnection>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<NhiTicketLedger> NhiTicketLedgers => Set<NhiTicketLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseOpenIddict();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Guid))
                {
                    property.SetValueConverter(LowercaseGuidConverter);
                    property.SetCollation("NOCASE");
                }
            }
        }
    }
}
