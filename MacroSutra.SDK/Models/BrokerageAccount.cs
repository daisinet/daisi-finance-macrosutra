namespace MacroSutra.SDK.Models;

public class BrokerageAccount
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public bool IsPaperTrading { get; set; }
    public decimal? CachedBalance { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Write-only credential data for account creation/update.
    /// Stripped from API responses.
    /// </summary>
    public string? CredentialData { get; set; }
}
