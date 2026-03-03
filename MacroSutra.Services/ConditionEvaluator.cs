using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Services;

/// <summary>
/// Evaluates a single TriggerCondition against live market data.
/// Supports CrossesAbove/CrossesBelow via in-memory previous-value tracking.
/// </summary>
public class ConditionEvaluator
{
    /// <summary>
    /// Evaluates a condition and returns whether it triggered and the current value.
    /// </summary>
    /// <param name="condition">The trigger condition to evaluate.</param>
    /// <param name="snapshot">Current market data snapshot.</param>
    /// <param name="priceHistory">Historical close prices (most recent last).</param>
    /// <param name="previousValues">Mutable dictionary for crossover tracking. Key: conditionId+symbol.</param>
    public virtual (bool triggered, decimal currentValue) Evaluate(
        TriggerCondition condition,
        MarketSnapshot snapshot,
        decimal[] priceHistory,
        Dictionary<string, decimal> previousValues)
    {
        var currentValue = ResolveCurrentValue(condition, snapshot, priceHistory);
        var crossoverKey = $"{condition.ConditionId}:{snapshot.Symbol}";
        var triggered = EvaluateOperator(condition.Operator, currentValue, condition.Value, crossoverKey, previousValues);

        // Always update previous value for crossover tracking
        previousValues[crossoverKey] = currentValue;

        return (triggered, currentValue);
    }

    internal static decimal ResolveCurrentValue(TriggerCondition condition, MarketSnapshot snapshot, decimal[] priceHistory)
    {
        return condition.ConditionType switch
        {
            ConditionType.Price => snapshot.Price,
            ConditionType.Volume => snapshot.Volume,
            ConditionType.PercentChange => snapshot.DailyChangePercent,
            ConditionType.MovingAverage => priceHistory.Length >= (condition.Period ?? 20)
                ? TechnicalIndicators.SMA(priceHistory, condition.Period ?? 20)
                : 0,
            ConditionType.RSI => priceHistory.Length >= (condition.Period ?? 14) + 1
                ? TechnicalIndicators.RSI(priceHistory, condition.Period ?? 14)
                : 50, // neutral default
            ConditionType.MACD => priceHistory.Length >= 26
                ? TechnicalIndicators.MACD(priceHistory).macdLine
                : 0,
            _ => 0
        };
    }

    internal static bool EvaluateOperator(
        ConditionOperator op, decimal currentValue, decimal targetValue,
        string crossoverKey, Dictionary<string, decimal> previousValues)
    {
        return op switch
        {
            ConditionOperator.GreaterThan => currentValue > targetValue,
            ConditionOperator.GreaterThanOrEqual => currentValue >= targetValue,
            ConditionOperator.LessThan => currentValue < targetValue,
            ConditionOperator.LessThanOrEqual => currentValue <= targetValue,
            ConditionOperator.Equal => currentValue == targetValue,
            ConditionOperator.CrossesAbove => EvaluateCrossesAbove(currentValue, targetValue, crossoverKey, previousValues),
            ConditionOperator.CrossesBelow => EvaluateCrossesBelow(currentValue, targetValue, crossoverKey, previousValues),
            _ => false
        };
    }

    private static bool EvaluateCrossesAbove(decimal current, decimal target, string key, Dictionary<string, decimal> prev)
    {
        if (!prev.TryGetValue(key, out var previous))
            return false; // No previous value — can't detect a crossing
        return previous <= target && current > target;
    }

    private static bool EvaluateCrossesBelow(decimal current, decimal target, string key, Dictionary<string, decimal> prev)
    {
        if (!prev.TryGetValue(key, out var previous))
            return false;
        return previous >= target && current < target;
    }
}
