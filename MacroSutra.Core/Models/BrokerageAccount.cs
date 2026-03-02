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

    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
}
