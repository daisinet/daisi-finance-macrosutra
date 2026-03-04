using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// Records a single strategy trigger event with outcome tracking.
/// Stored in the StrategyPerformance container, partitioned by AccountId.
/// </summary>
public class StrategyTriggerRecord
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(StrategyTriggerRecord);
    public string AccountId { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public DateTime TriggeredUtc { get; set; } = DateTime.UtcNow;
    public List<string> TradeIds { get; set; } = new();
    public TriggerOutcome Outcome { get; set; } = TriggerOutcome.Open;
    public decimal? EntryPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    public decimal? PnL { get; set; }
    public decimal? ReturnPercent { get; set; }
}

public enum TriggerOutcome
{
    Open = 0,
    Win = 1,
    Loss = 2
}
