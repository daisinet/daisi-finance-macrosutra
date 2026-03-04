namespace MacroSutra.Core.Models;

/// <summary>
/// A single entry on the strategy leaderboard.
/// Computed from backtest data and community stats, not persisted.
/// </summary>
public class LeaderboardEntry
{
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AccountId { get; set; } = "";
    public List<string> Symbols { get; set; } = new();
    public decimal TotalReturnPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal WinRate { get; set; }
    public int TotalBacktests { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int SubscriberCount { get; set; }
}
