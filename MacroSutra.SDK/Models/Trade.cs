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
    public string? BrokerageAccountId { get; set; }
    public OptionDetailsDto? OptionDetails { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? FilledUtc { get; set; }
}

public class OptionDetailsDto
{
    public string ContractSymbol { get; set; } = "";
    public string UnderlyingSymbol { get; set; } = "";
    public string OptionType { get; set; } = "";
    public string ExpirationDate { get; set; } = "";
    public decimal StrikePrice { get; set; }
    public int Contracts { get; set; }
    public decimal? PremiumPerShare { get; set; }
}
