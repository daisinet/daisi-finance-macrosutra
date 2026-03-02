using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// An action to take when a strategy triggers (e.g. place an order).
/// Embedded as a nested item inside TradingStrategy.
/// </summary>
public class TradeAction
{
    public string ActionId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public TradeActionType ActionType { get; set; } = TradeActionType.MarketOrder;
    public TradeSide Side { get; set; } = TradeSide.Buy;
    public QuantityType QuantityType { get; set; } = QuantityType.Shares;
    public decimal Quantity { get; set; }

    /// <summary>
    /// Limit price for LimitOrder/StopLimitOrder types.
    /// </summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>
    /// Stop price for StopOrder/StopLimitOrder types.
    /// </summary>
    public decimal? StopPrice { get; set; }
}
