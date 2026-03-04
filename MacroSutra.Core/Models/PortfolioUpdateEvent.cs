namespace MacroSutra.Core.Models;

/// <summary>
/// Event raised when a portfolio position or balance changes.
/// Pushed to connected clients via SignalR.
/// </summary>
public class PortfolioUpdateEvent
{
    public string AccountId { get; set; } = "";
    public string? BrokerageAccountId { get; set; }
    public int PositionCount { get; set; }
    public decimal? Balance { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
