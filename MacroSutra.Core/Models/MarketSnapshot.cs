namespace MacroSutra.Core.Models;

/// <summary>
/// Point-in-time market data snapshot for a symbol.
/// Returned by MarketDataService from Alpaca's free data API.
/// </summary>
public class MarketSnapshot
{
    public string Symbol { get; set; } = "";
    public decimal Price { get; set; }
    public long Volume { get; set; }
    public decimal DailyChangePercent { get; set; }
    public decimal DailyHigh { get; set; }
    public decimal DailyLow { get; set; }
    public decimal PreviousClose { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
