using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// Event raised when a strategy triggers and executes trades.
/// Pushed to connected clients via SignalR.
/// </summary>
public class StrategyAlertEvent
{
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
    public DateTime TriggeredUtc { get; set; } = DateTime.UtcNow;
    public List<string> TradeIds { get; set; } = new();
    public TradeSide TradeSide { get; set; }
    public decimal Quantity { get; set; }
    public string AccountId { get; set; } = "";
}
