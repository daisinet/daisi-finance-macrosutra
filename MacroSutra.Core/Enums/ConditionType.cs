namespace MacroSutra.Core.Enums;

/// <summary>
/// The type of data a trigger condition evaluates.
/// </summary>
public enum ConditionType
{
    Price = 0,
    Volume = 1,
    PercentChange = 2,
    MovingAverage = 3,
    RSI = 4,
    MACD = 5,
    Custom = 6,
    TimeOfDay = 7,
    DayOfWeek = 8
}
