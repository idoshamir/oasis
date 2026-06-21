namespace JiraIntegration.Server.Data.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;

    public JiraConnection? JiraConnection { get; set; }
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
    public ICollection<NhiTicketLedger> NhiTicketLedgers { get; set; } = [];
}
