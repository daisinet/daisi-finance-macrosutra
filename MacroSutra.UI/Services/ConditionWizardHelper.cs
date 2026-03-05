using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MudBlazor;

namespace MacroSutra.UI.Services;

/// <summary>
/// Testable helper for the condition wizard: operators per type, input modes, natural language descriptions.
/// </summary>
public static class ConditionWizardHelper
{
    public enum InputMode { Numeric, TimePicker, DaySelector }

    public record ConditionTypeInfo(
        ConditionType Type,
        string Name,
        string Description,
        string Icon,
        string Category);

    private static readonly ConditionTypeInfo[] AllTypes =
    [
        new(ConditionType.Price, "Price", "Current stock price", Icons.Material.Filled.AttachMoney, "Market Data"),
        new(ConditionType.Volume, "Volume", "Trading volume", Icons.Material.Filled.BarChart, "Market Data"),
        new(ConditionType.PercentChange, "% Change", "Percent change from previous close", Icons.Material.Filled.TrendingUp, "Market Data"),
        new(ConditionType.MovingAverage, "Moving Average", "Simple or exponential moving average", Icons.Material.Filled.ShowChart, "Technical Indicators"),
        new(ConditionType.RSI, "RSI", "Relative Strength Index (0-100)", Icons.Material.Filled.Speed, "Technical Indicators"),
        new(ConditionType.MACD, "MACD", "Moving Average Convergence Divergence", Icons.Material.Filled.MultilineChart, "Technical Indicators"),
        new(ConditionType.TimeOfDay, "Time of Day", "Eastern Time market hours", Icons.Material.Filled.Schedule, "Time-Based"),
        new(ConditionType.DayOfWeek, "Day of Week", "Specific trading day", Icons.Material.Filled.CalendarMonth, "Time-Based"),
        new(ConditionType.Custom, "Custom", "Custom condition expression", Icons.Material.Filled.Code, "Market Data"),
    ];

    /// <summary>
    /// Returns info for all condition types.
    /// </summary>
    public static ConditionTypeInfo[] GetAllConditionTypes() => AllTypes;

    /// <summary>
    /// Returns info for a specific condition type.
    /// </summary>
    public static ConditionTypeInfo GetConditionTypeInfo(ConditionType type)
        => AllTypes.FirstOrDefault(t => t.Type == type)
           ?? new ConditionTypeInfo(type, type.ToString(), "", Icons.Material.Filled.HelpOutline, "Other");

    /// <summary>
    /// Returns the valid operators for a given condition type.
    /// </summary>
    public static ConditionOperator[] GetOperatorsForType(ConditionType type) => type switch
    {
        ConditionType.TimeOfDay => [ConditionOperator.GreaterThan, ConditionOperator.LessThan, ConditionOperator.GreaterThanOrEqual, ConditionOperator.LessThanOrEqual, ConditionOperator.Equal],
        ConditionType.DayOfWeek => [ConditionOperator.Equal],
        _ => Enum.GetValues<ConditionOperator>()
    };

    /// <summary>
    /// Returns the input mode for a given condition type.
    /// </summary>
    public static InputMode GetInputMode(ConditionType type) => type switch
    {
        ConditionType.TimeOfDay => InputMode.TimePicker,
        ConditionType.DayOfWeek => InputMode.DaySelector,
        _ => InputMode.Numeric
    };

    /// <summary>
    /// Whether this condition type requires a period parameter.
    /// </summary>
    public static bool RequiresPeriod(ConditionType type) => type switch
    {
        ConditionType.MovingAverage or ConditionType.RSI or ConditionType.MACD => true,
        _ => false
    };

    /// <summary>
    /// Returns a human-readable description of a condition.
    /// </summary>
    public static string DescribeCondition(TriggerCondition condition)
    {
        var typeName = GetConditionTypeInfo(condition.ConditionType).Name;
        var opStr = DescribeOperator(condition.Operator, condition.ConditionType);
        var valueStr = DescribeValue(condition.Value, condition.ConditionType);
        var periodStr = condition.Period.HasValue && RequiresPeriod(condition.ConditionType)
            ? $" ({condition.Period}p)"
            : "";

        return $"{typeName} {opStr} {valueStr}{periodStr}";
    }

    /// <summary>
    /// Returns a human-readable operator description.
    /// </summary>
    public static string DescribeOperator(ConditionOperator op, ConditionType type)
    {
        if (type == ConditionType.TimeOfDay)
        {
            return op switch
            {
                ConditionOperator.GreaterThan => "after",
                ConditionOperator.LessThan => "before",
                ConditionOperator.Equal => "at",
                ConditionOperator.GreaterThanOrEqual => "at or after",
                ConditionOperator.LessThanOrEqual => "at or before",
                _ => op.ToString()
            };
        }

        return op switch
        {
            ConditionOperator.GreaterThan => ">",
            ConditionOperator.GreaterThanOrEqual => ">=",
            ConditionOperator.LessThan => "<",
            ConditionOperator.LessThanOrEqual => "<=",
            ConditionOperator.Equal => "=",
            ConditionOperator.CrossesAbove => "crosses above",
            ConditionOperator.CrossesBelow => "crosses below",
            _ => op.ToString()
        };
    }

    private static string DescribeValue(decimal value, ConditionType type) => type switch
    {
        ConditionType.TimeOfDay => FormatTimeValue(value),
        ConditionType.DayOfWeek => ((DayOfWeek)(int)value).ToString(),
        ConditionType.PercentChange => $"{value:N2}%",
        ConditionType.RSI => $"{value:N0}",
        _ => $"{value:N2}"
    };

    private static string FormatTimeValue(decimal minuteValue)
    {
        var totalMinutes = (int)minuteValue;
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        var period = hours >= 12 ? "PM" : "AM";
        var displayHour = hours > 12 ? hours - 12 : (hours == 0 ? 12 : hours);
        return $"{displayHour}:{minutes:D2} {period}";
    }

    /// <summary>
    /// Returns the display color for a condition type (CSS class suffix).
    /// </summary>
    public static string GetConditionColor(ConditionType type) => type switch
    {
        ConditionType.Price or ConditionType.Volume or ConditionType.PercentChange => "market-data",
        ConditionType.MovingAverage or ConditionType.RSI or ConditionType.MACD => "technical",
        ConditionType.TimeOfDay or ConditionType.DayOfWeek => "time-based",
        _ => "custom"
    };

    /// <summary>
    /// Parses a drag payload string into its kind and value.
    /// Format: "condition:{ConditionType}" or "action:{ActionType}" or "group"
    /// </summary>
    public static (string Kind, string Value) ParseDragPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return ("", "");

        var parts = payload.Split(':', 2);
        return parts.Length == 2
            ? (parts[0].Trim().ToLowerInvariant(), parts[1].Trim())
            : (parts[0].Trim().ToLowerInvariant(), "");
    }
}
