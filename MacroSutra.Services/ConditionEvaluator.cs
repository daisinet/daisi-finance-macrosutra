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

    private static readonly TimeZoneInfo EasternTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

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
            ConditionType.TimeOfDay => ResolveTimeOfDay(snapshot.Timestamp),
            ConditionType.DayOfWeek => ResolveDayOfWeek(snapshot.Timestamp),
            _ => 0
        };
    }

    /// <summary>
    /// Returns minutes since midnight in Eastern Time. Uses snapshot.Timestamp so backtesting works correctly.
    /// </summary>
    private static decimal ResolveTimeOfDay(DateTime timestamp)
    {
        var et = TimeZoneInfo.ConvertTimeFromUtc(
            timestamp.Kind == DateTimeKind.Utc ? timestamp : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            EasternTime);
        return et.Hour * 60 + et.Minute;
    }

    /// <summary>
    /// Returns day of week as 0=Sunday through 6=Saturday.
    /// </summary>
    private static decimal ResolveDayOfWeek(DateTime timestamp)
    {
        var et = TimeZoneInfo.ConvertTimeFromUtc(
            timestamp.Kind == DateTimeKind.Utc ? timestamp : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            EasternTime);
        return (int)et.DayOfWeek;
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

    /// <summary>
    /// Recursively evaluates a nested ConditionGroup tree.
    /// Returns whether the group as a whole triggered plus individual condition results.
    /// </summary>
    public virtual (bool triggered, List<(bool passed, decimal value, string conditionId)> results) EvaluateGroup(
        ConditionGroup group,
        MarketSnapshot snapshot,
        decimal[] priceHistory,
        Dictionary<string, decimal> previousValues)
    {
        var allResults = new List<(bool passed, decimal value, string conditionId)>();

        // Evaluate direct conditions in this group
        var localResults = new List<bool>();
        foreach (var condition in group.Conditions)
        {
            var (triggered, currentValue) = Evaluate(condition, snapshot, priceHistory, previousValues);
            allResults.Add((triggered, currentValue, condition.ConditionId));
            localResults.Add(triggered);
        }

        // Recurse into child groups
        foreach (var childGroup in group.ChildGroups)
        {
            var (childTriggered, childResults) = EvaluateGroup(childGroup, snapshot, priceHistory, previousValues);
            allResults.AddRange(childResults);
            localResults.Add(childTriggered);
        }

        // Apply this group's logic
        bool groupTriggered = group.Logic switch
        {
            LogicGroupType.And => localResults.Count > 0 && localResults.All(r => r),
            LogicGroupType.Or => localResults.Any(r => r),
            _ => false
        };

        return (groupTriggered, allResults);
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
