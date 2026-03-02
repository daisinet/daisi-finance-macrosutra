namespace MacroSutra.Core.Models;

/// <summary>
/// A current position (holding) in a brokerage account.
/// Stored in the Portfolios container, partitioned by AccountId.
/// </summary>
public class Position
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(Position);
    public string AccountId { get; set; } = "";
    public string BrokerageAccountId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? MarketValue => CurrentPrice.HasValue ? CurrentPrice.Value * Quantity : null;
    public decimal? UnrealizedPnL => MarketValue.HasValue ? MarketValue.Value - (AverageCost * Quantity) : null;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
}
