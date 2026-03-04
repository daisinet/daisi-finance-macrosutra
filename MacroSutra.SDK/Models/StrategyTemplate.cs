namespace MacroSutra.SDK.Models;

/// <summary>
/// A pre-built strategy template with conditions and actions ready to use.
/// </summary>
public class StrategyTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public List<TemplateCondition> Conditions { get; set; } = new();
    public List<TemplateAction> Actions { get; set; } = new();
    public string LogicGroup { get; set; } = "And";
}

public class TemplateCondition
{
    public string ConditionType { get; set; } = "";
    public string Operator { get; set; } = "";
    public decimal Value { get; set; }
    public int? Period { get; set; }
}

public class TemplateAction
{
    public string ActionType { get; set; } = "";
    public string Side { get; set; } = "";
    public string QuantityType { get; set; } = "";
    public decimal Quantity { get; set; }
}
