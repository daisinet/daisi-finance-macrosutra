namespace MacroSutra.Core.Models;

/// <summary>
/// Denormalized community statistics for a strategy.
/// Stored in the Community container, partitioned by StrategyId.
/// The id matches the StrategyId for easy point reads.
/// </summary>
public class StrategyCommunityStats
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(StrategyCommunityStats);
    public string StrategyId { get; set; } = "";
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int SubscriberCount { get; set; }
    public int ForkCount { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
