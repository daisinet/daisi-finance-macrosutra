using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A linked brokerage account for trade execution.
/// Stored in the Portfolios container, partitioned by AccountId.
/// </summary>
public class BrokerageAccount
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(BrokerageAccount);
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public BrokerageProvider Provider { get; set; } = BrokerageProvider.Paper;

    /// <summary>
    /// Encrypted credentials or API key reference for the brokerage.
    /// </summary>
    public string CredentialRef { get; set; } = "";

    /// <summary>
    /// Provider-specific credentials stored as JSON.
    /// </summary>
    public string CredentialData { get; set; } = "";

    /// <summary>
    /// Whether this account uses paper/sandbox mode.
    /// </summary>
    public bool IsPaperTrading { get; set; }

    /// <summary>
    /// Last-synced account balance from the brokerage.
    /// </summary>
    public decimal? CachedBalance { get; set; }

    /// <summary>
    /// When the account was last synced with the brokerage.
    /// </summary>
    public DateTime? LastSyncUtc { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
}
