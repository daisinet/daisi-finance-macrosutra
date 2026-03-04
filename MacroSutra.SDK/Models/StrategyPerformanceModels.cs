namespace MacroSutra.SDK.Models;

public class StrategyTriggerRecord
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public DateTime TriggeredUtc { get; set; }
    public List<string> TradeIds { get; set; } = new();
    public string Outcome { get; set; } = "Open";
    public decimal? EntryPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    public decimal? PnL { get; set; }
    public decimal? ReturnPercent { get; set; }
}

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
