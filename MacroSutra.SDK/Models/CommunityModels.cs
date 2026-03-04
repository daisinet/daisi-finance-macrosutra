namespace MacroSutra.SDK.Models;

public class StrategyReview
{
    public string Id { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string ReviewerAccountId { get; set; } = "";
    public string ReviewerName { get; set; } = "";
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class StrategyCommunityStats
{
    public string StrategyId { get; set; } = "";
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int SubscriberCount { get; set; }
    public int ForkCount { get; set; }
}

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

public class CreateReviewRequest
{
    public int Rating { get; set; }
    public string? Text { get; set; }
}
