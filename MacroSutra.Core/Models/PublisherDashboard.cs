namespace MacroSutra.Core.Models;

/// <summary>
/// Dashboard data for a strategy publisher showing subscriber stats and recent actions.
/// </summary>
public class PublisherDashboard
{
    public int ActiveSubscribers { get; set; }
    public int TotalSubscribers { get; set; }
    public long TotalCreditsEarned { get; set; }
    public List<PublisherStrategyStats> Strategies { get; set; } = new();
    public List<SubscriptionAction> RecentActions { get; set; } = new();
}

/// <summary>
/// Per-strategy subscriber stats for the publisher dashboard.
/// </summary>
public class PublisherStrategyStats
{
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public int ActiveSubscribers { get; set; }
    public long CreditPrice { get; set; }
}
