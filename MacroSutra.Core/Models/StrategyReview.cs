namespace MacroSutra.Core.Models;

/// <summary>
/// A user review/rating of a public strategy.
/// Stored in the Community container, partitioned by StrategyId.
/// </summary>
public class StrategyReview
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(StrategyReview);
    public string StrategyId { get; set; } = "";
    public string ReviewerAccountId { get; set; } = "";
    public string ReviewerUserId { get; set; } = "";
    public string ReviewerName { get; set; } = "";
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
}
