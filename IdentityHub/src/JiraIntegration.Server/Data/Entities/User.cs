using Microsoft.AspNetCore.Identity;

namespace JiraIntegration.Server.Data.Entities;

public sealed class User : IdentityUser<Guid>
{
    public string? LegacySalt { get; set; }
    public string? LegacyPasswordHash { get; set; }

    public JiraConnection? JiraConnection { get; set; }
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
    public ICollection<NhiTicketLedger> NhiTicketLedgers { get; set; } = [];
}
