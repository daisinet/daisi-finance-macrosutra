namespace MacroSutra.SDK.Models;

public class OptionsOrderRequest
{
    public string BrokerageAccountId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public string OrderType { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
}
