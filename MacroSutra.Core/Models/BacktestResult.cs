using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// Backtest result stored as a Cosmos document in the Backtests container.
/// Contains metrics, equity curve, and simulated trades.
/// </summary>
public class BacktestResult
{
    public string id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public BacktestStatus Status { get; set; }
    public BacktestMetrics? Metrics { get; set; }
    public List<EquityCurvePoint> EquityCurve { get; set; } = new();
    public List<SimulatedTrade> Trades { get; set; } = new();
    public decimal SlippageBps { get; set; }
    public decimal CommissionPerTrade { get; set; }
    public string? TimeFrame { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string Type { get; set; } = "BacktestResult";
}

/// <summary>
/// Aggregate performance metrics for a completed backtest.
/// </summary>
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
    public decimal AverageTradeReturnPercent { get; set; }
    public TimeSpan AverageTradeDuration { get; set; }
    public decimal BestTradePercent { get; set; }
    public decimal WorstTradePercent { get; set; }
}

/// <summary>
/// A single point on the equity curve, recorded once per bar.
/// </summary>
public class EquityCurvePoint
{
    public DateOnly Date { get; set; }
    /// <summary>
    /// Precise timestamp for intraday equity curve points.
    /// </summary>
    public DateTime? Timestamp { get; set; }
    public decimal Equity { get; set; }
    /// <summary>
    /// Negative percentage from peak equity (e.g. -5.2 means 5.2% drawdown).
    /// </summary>
    public decimal Drawdown { get; set; }
}

/// <summary>
/// A simulated trade within a backtest (entry and optional exit).
/// </summary>
public class SimulatedTrade
{
    public string Symbol { get; set; } = "";
    public TradeSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    public DateOnly EntryDate { get; set; }
    public DateOnly? ExitDate { get; set; }
    public decimal? PnL { get; set; }
    public decimal? ReturnPercent { get; set; }
    public string TriggerReason { get; set; } = "";
}
