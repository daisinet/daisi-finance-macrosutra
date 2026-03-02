namespace MacroSutra.Core.Enums;

/// <summary>
/// The type of action to take when a strategy triggers.
/// </summary>
public enum TradeActionType
{
    MarketOrder = 0,
    LimitOrder = 1,
    StopOrder = 2,
    StopLimitOrder = 3,
    Alert = 4
}
