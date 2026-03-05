namespace MacroSutra.SDK.Models;

public class TradingStrategy
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Symbols { get; set; } = new();
    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastEvaluatedUtc { get; set; }
    public DateTime? LastTriggeredUtc { get; set; }

    // Trigger groups (each with its own conditions and actions)
    public List<SdkTriggerGroup> TriggerGroups { get; set; } = new();

    // Strategy configuration
    public string SizingMode { get; set; } = "Fixed";
    public string Visibility { get; set; } = "Private";
    public long SubscriptionCreditPrice { get; set; }
    public int SubscriptionPeriodDays { get; set; } = 30;
    public string? ForkedFromStrategyId { get; set; }
}

public class SdkTriggerGroup
{
    public string GroupId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Interval { get; set; } = "Day";
    public string Logic { get; set; } = "And";
    public List<StrategyCondition> Conditions { get; set; } = new();
    public List<StrategyAction> Actions { get; set; } = new();
}

public class StrategyCondition
{
    public string ConditionId { get; set; } = "";
    public string ConditionType { get; set; } = "Price";
    public string Operator { get; set; } = "GreaterThan";
    public decimal Value { get; set; }
    public int? Period { get; set; }
}

public class StrategyAction
{
    public string ActionType { get; set; } = "MarketOrder";
    public string Side { get; set; } = "Buy";
    public string QuantityType { get; set; } = "Shares";
    public decimal Quantity { get; set; }
}
