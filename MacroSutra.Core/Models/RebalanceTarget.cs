namespace MacroSutra.Core.Models;

/// <summary>
/// Target allocation for portfolio rebalancing.
/// Stored in the RebalanceTargets container, partitioned by AccountId.
/// </summary>
public class RebalanceTarget
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(RebalanceTarget);
    public string AccountId { get; set; } = "";
    public string Name { get; set; } = "";
    public string BrokerageAccountId { get; set; } = "";
    public List<AllocationTarget> Allocations { get; set; } = new();
    public decimal DriftThresholdPercent { get; set; } = 5m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single target allocation within a rebalance target.
/// </summary>
public class AllocationTarget
{
    public string Symbol { get; set; } = "";
    public decimal TargetPercent { get; set; }
}
