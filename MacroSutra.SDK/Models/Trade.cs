namespace MacroSutra.SDK.Models;

public class Trade
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public string OrderType { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal? FilledPrice { get; set; }
    public decimal? FilledQuantity { get; set; }
    public string Status { get; set; } = "";
    public string? StrategyId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? FilledUtc { get; set; }
}
