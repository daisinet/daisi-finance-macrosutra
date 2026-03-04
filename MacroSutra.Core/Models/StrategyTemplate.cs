using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A pre-built strategy template that users can apply to quickly create strategies.
/// Templates are code-defined, not stored in the database.
/// </summary>
public class StrategyTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public LogicGroupType LogicGroup { get; set; } = LogicGroupType.And;
    public List<TriggerCondition> Conditions { get; set; } = new();
    public List<TradeAction> Actions { get; set; } = new();
}
