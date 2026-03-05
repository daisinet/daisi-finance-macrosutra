namespace MacroSutra.Core.Models;

/// <summary>
/// Result of analyzing portfolio drift against a rebalance target.
/// </summary>
public class RebalanceAnalysis
{
    public bool NeedsRebalancing { get; set; }
    public decimal TotalPortfolioValue { get; set; }
    public List<AllocationDrift> Drifts { get; set; } = new();
}

/// <summary>
/// Drift information for a single symbol in a rebalance analysis.
/// </summary>
public class AllocationDrift
{
    public string Symbol { get; set; } = "";
    public decimal ActualPercent { get; set; }
    public decimal TargetPercent { get; set; }
    public decimal DriftPercent { get; set; }
    public decimal SuggestedTradeValue { get; set; }
}
