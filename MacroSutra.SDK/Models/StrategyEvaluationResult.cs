namespace MacroSutra.SDK.Models;

/// <summary>
/// Result of evaluating a strategy's conditions against live market data.
/// </summary>
public class StrategyEvaluationResult
{
    public bool WouldTrigger { get; set; }
    public List<ConditionResult> Conditions { get; set; } = new();
    public DateTime EvaluatedUtc { get; set; }
}

/// <summary>
/// Result of evaluating a single trigger condition.
/// </summary>
public class ConditionResult
{
    public string ConditionId { get; set; } = "";
    public string ConditionType { get; set; } = "";
    public decimal CurrentValue { get; set; }
    public decimal TargetValue { get; set; }
    public string Operator { get; set; } = "";
    public bool Passed { get; set; }
}
