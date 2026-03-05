using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A named group of conditions paired with actions.
/// Each trigger group evaluates independently — when its conditions are met, its actions fire.
/// A strategy can have multiple trigger groups (e.g. "Buy Signal" and "Sell Signal").
/// </summary>
public class TriggerGroup
{
    public string GroupId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public BarTimeFrame Interval { get; set; } = BarTimeFrame.Day;
    public ConditionGroup Conditions { get; set; } = new();
    public List<TradeAction> Actions { get; set; } = new();
}
