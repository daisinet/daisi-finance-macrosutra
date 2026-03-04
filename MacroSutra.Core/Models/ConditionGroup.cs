using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A group of conditions combined with AND/OR logic, supporting nesting for compound triggers.
/// When a strategy uses RootConditionGroup, evaluation is recursive through the group tree.
/// </summary>
public class ConditionGroup
{
    public string GroupId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public LogicGroupType Logic { get; set; } = LogicGroupType.And;
    public List<TriggerCondition> Conditions { get; set; } = new();
    public List<ConditionGroup> ChildGroups { get; set; } = new();
}
