using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A record of an action taken on behalf of a subscription (e.g. a mirrored trade).
/// Embedded history or stored alongside Subscription.
/// </summary>
public class SubscriptionAction
{
    public string ActionId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string SubscriptionId { get; set; } = "";
    public string? TradeId { get; set; }
    public SubscriptionActionType ActionType { get; set; } = SubscriptionActionType.Mirror;
    public string Symbol { get; set; } = "";
    public TradeSide Side { get; set; } = TradeSide.Buy;
    public decimal Quantity { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedUtc { get; set; } = DateTime.UtcNow;
}
