namespace MacroSutra.Core.Models;

/// <summary>
/// Computed performance summary for a strategy based on trigger records.
/// </summary>
public class StrategyPerformanceSummary
{
    public int TotalTriggers { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int OpenTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalPnL { get; set; }
    public List<MonthlyReturn> MonthlyReturns { get; set; } = new();
}

public class MonthlyReturn
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ReturnPercent { get; set; }
    public int Triggers { get; set; }
}
