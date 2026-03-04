namespace MacroSutra.SDK.Models;

public class BacktestResult
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal InitialCapital { get; set; }
    public decimal SlippageBps { get; set; }
    public decimal CommissionPerTrade { get; set; }
    public string? TimeFrame { get; set; }
    public string Status { get; set; } = "";
    public BacktestMetrics? Metrics { get; set; }
    public List<BacktestEquityCurvePoint> EquityCurve { get; set; } = new();
    public DateTime CreatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BacktestEquityCurvePoint
{
    public string Date { get; set; } = "";
    public decimal Equity { get; set; }
    public decimal Drawdown { get; set; }
}

public class BacktestMetrics
{
    public decimal TotalReturnPercent { get; set; }
    public decimal FinalEquity { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
}

public class BacktestRequest
{
    public string StrategyId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal InitialCapital { get; set; }
    public decimal SlippageBps { get; set; }
    public decimal CommissionPerTrade { get; set; }
    public string? TimeFrame { get; set; }
}

public class WalkForwardRequest
{
    public string StrategyId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal InitialCapital { get; set; }
    public int InSampleDays { get; set; } = 252;
    public int OutOfSampleDays { get; set; } = 63;
    public decimal SlippageBps { get; set; }
    public decimal CommissionPerTrade { get; set; }
}

public class WalkForwardResult
{
    public string Id { get; set; } = "";
    public List<WalkForwardWindow> Windows { get; set; } = new();
    public WalkForwardSummary? Summary { get; set; }
}

public class WalkForwardWindow
{
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public bool IsInSample { get; set; }
    public decimal TotalReturnPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public int TotalTrades { get; set; }
}

public class WalkForwardSummary
{
    public decimal AverageOosSharpe { get; set; }
    public decimal AverageOosReturn { get; set; }
    public decimal ConsistencyScore { get; set; }
    public int TotalWindows { get; set; }
    public int ProfitableOosWindows { get; set; }
}
