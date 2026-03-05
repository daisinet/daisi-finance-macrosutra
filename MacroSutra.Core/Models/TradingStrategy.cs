using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A trading strategy with trigger conditions and actions.
/// Stored in the Strategies container, partitioned by AccountId.
/// Conditions and actions are embedded as nested lists.
/// </summary>
public class TradingStrategy
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(TradingStrategy);
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    /// Symbols this strategy monitors (e.g. ["AAPL", "MSFT"]).
    /// </summary>
    public List<string> Symbols { get; set; } = new();

    /// <summary>
    /// Named trigger groups, each with its own conditions and actions.
    /// Each group evaluates independently — when its conditions are met, its actions fire.
    /// </summary>
    public List<TriggerGroup> TriggerGroups { get; set; } = new();
    public SizingMode SizingMode { get; set; } = SizingMode.Fixed;

    /// <summary>
    /// Optional brokerage account to execute trades through.
    /// </summary>
    public string? BrokerageAccountId { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Strategy visibility: Private, Public, or SubscribersOnly.
    /// </summary>
    public StrategyVisibility Visibility { get; set; } = StrategyVisibility.Private;

    /// <summary>
    /// Backward-compatible computed property.
    /// Reading: true when Visibility is Public.
    /// Writing: sets Visibility to Public (true) or Private (false).
    /// </summary>
    public bool IsPublic
    {
        get => Visibility == StrategyVisibility.Public;
        set => Visibility = value ? StrategyVisibility.Public : StrategyVisibility.Private;
    }

    /// <summary>
    /// If this strategy was forked, the id of the original strategy.
    /// </summary>
    public string? ForkedFromStrategyId { get; set; }

    /// <summary>
    /// If this strategy was forked, the account id of the original author.
    /// </summary>
    public string? ForkedFromAccountId { get; set; }

    /// <summary>
    /// Credit price per subscription period (publisher-set, 0 = free).
    /// </summary>
    public long SubscriptionCreditPrice { get; set; }

    /// <summary>
    /// Subscription billing period in days (default 30).
    /// </summary>
    public int SubscriptionPeriodDays { get; set; } = 30;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>
    /// When the evaluation engine last checked this strategy.
    /// </summary>
    public DateTime? LastEvaluatedUtc { get; set; }

    /// <summary>
    /// When this strategy's conditions last triggered and executed actions.
    /// </summary>
    public DateTime? LastTriggeredUtc { get; set; }
}
