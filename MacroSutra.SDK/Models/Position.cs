namespace MacroSutra.SDK.Models;

public class Position
{
    public string Id { get; set; } = "";
    public string BrokerageAccountId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? MarketValue { get; set; }
    public decimal? UnrealizedPnL { get; set; }
}
