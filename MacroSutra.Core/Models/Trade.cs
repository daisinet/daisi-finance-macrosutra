using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A recorded trade execution or order.
/// Stored in the Trades container, partitioned by AccountId.
/// </summary>
public class Trade
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(Trade);
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";

    /// <summary>
    /// The strategy that triggered this trade, if any.
    /// </summary>
    public string? StrategyId { get; set; }

    public string? BrokerageAccountId { get; set; }
    public string Symbol { get; set; } = "";
    public TradeSide Side { get; set; } = TradeSide.Buy;
    public TradeActionType OrderType { get; set; } = TradeActionType.MarketOrder;
    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }
    public decimal? FilledPrice { get; set; }
    public decimal? FilledQuantity { get; set; }
    public TradeStatus Status { get; set; } = TradeStatus.Pending;

    /// <summary>
    /// External order ID from the brokerage.
    /// </summary>
    public string? ExternalOrderId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
    public DateTime? FilledUtc { get; set; }
}
