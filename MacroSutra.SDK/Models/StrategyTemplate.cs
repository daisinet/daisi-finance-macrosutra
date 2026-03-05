namespace MacroSutra.SDK.Models;

/// <summary>
/// A pre-built strategy template with trigger groups ready to use.
/// </summary>
public class StrategyTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public List<SdkTriggerGroup> TriggerGroups { get; set; } = new();
}
