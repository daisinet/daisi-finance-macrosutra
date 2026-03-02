using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A single condition within a trading strategy trigger.
/// Embedded as a nested item inside TradingStrategy.
/// </summary>
public class TriggerCondition
{
    public string ConditionId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public ConditionType ConditionType { get; set; } = ConditionType.Price;
    public ConditionOperator Operator { get; set; } = ConditionOperator.GreaterThan;
    public decimal Value { get; set; }

    /// <summary>
    /// Optional parameter for indicators (e.g. period length for RSI/MA).
    /// </summary>
    public int? Period { get; set; }
}
