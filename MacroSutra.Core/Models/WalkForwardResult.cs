namespace MacroSutra.Core.Models;

/// <summary>
/// Result of a walk-forward analysis: rolling in-sample/out-of-sample backtests.
/// </summary>
public class WalkForwardResult
{
    public List<WalkForwardWindow> Windows { get; set; } = new();
    public WalkForwardSummary? Summary { get; set; }
}

/// <summary>
/// A single window (in-sample or out-of-sample) in a walk-forward analysis.
/// </summary>
public class WalkForwardWindow
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsInSample { get; set; }
    public decimal TotalReturnPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public int TotalTrades { get; set; }
}

/// <summary>
/// Aggregate summary across all out-of-sample windows.
/// </summary>
public class WalkForwardSummary
{
    public decimal AverageOosSharpe { get; set; }
    public decimal AverageOosReturn { get; set; }

    /// <summary>
    /// Consistency score: fraction of OOS windows that were profitable (0.0–1.0).
    /// </summary>
    public decimal ConsistencyScore { get; set; }

    public int TotalWindows { get; set; }
    public int ProfitableOosWindows { get; set; }
}
