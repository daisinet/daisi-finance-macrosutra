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

    public LogicGroupType LogicGroup { get; set; } = LogicGroupType.And;
    public List<TriggerCondition> Conditions { get; set; } = new();
    public List<TradeAction> Actions { get; set; } = new();
    public SizingMode SizingMode { get; set; } = SizingMode.Fixed;

    /// <summary>
    /// Optional brokerage account to execute trades through.
    /// </summary>
    public string? BrokerageAccountId { get; set; }

    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
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
