using JiraIntegration.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JiraIntegration.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<Guid, string> LowercaseGuidConverter = new(
        guid => guid.ToString("D").ToLowerInvariant(),
        text => Guid.Parse(text));

    public DbSet<User> Users => Set<User>();
    public DbSet<JiraConnection> JiraConnections => Set<JiraConnection>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<NhiTicketLedger> NhiTicketLedgers => Set<NhiTicketLedger>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
