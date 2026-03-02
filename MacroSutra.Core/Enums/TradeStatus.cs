namespace MacroSutra.Core.Enums;

/// <summary>
/// Lifecycle status of a trade.
/// </summary>
public enum TradeStatus
{
    Pending = 0,
    Submitted = 1,
    PartiallyFilled = 2,
    Filled = 3,
    Cancelled = 4,
    Rejected = 5,
    Failed = 6
}
