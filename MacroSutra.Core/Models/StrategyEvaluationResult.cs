using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// Result of evaluating a strategy's conditions against live market data.
/// Used by the "Test Now" feature and the evaluation engine.
/// </summary>
public class StrategyEvaluationResult
{
    public bool WouldTrigger { get; set; }
    public List<ConditionResult> Conditions { get; set; } = new();
    public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of evaluating a single trigger condition.
/// </summary>
public class ConditionResult
{
    public string ConditionId { get; set; } = "";
    public ConditionType ConditionType { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal TargetValue { get; set; }
    public ConditionOperator Operator { get; set; }
    public bool Passed { get; set; }
}
