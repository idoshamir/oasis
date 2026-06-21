using JiraIntegration.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraIntegration.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<JiraConnection> JiraConnections => Set<JiraConnection>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<NhiTicketLedger> NhiTicketLedgers => Set<NhiTicketLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
