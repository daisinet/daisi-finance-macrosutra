using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A subscription to another user's published strategy.
/// Stored in the Subscriptions container, partitioned by AccountId (subscriber's).
/// </summary>
public class Subscription
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(Subscription);

    /// <summary>
    /// The subscriber's account ID (partition key).
    /// </summary>
    public string AccountId { get; set; } = "";

    public string SubscriberUserId { get; set; } = "";
    public string PublisherAccountId { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";

    public SubscriptionActionType ActionType { get; set; } = SubscriptionActionType.Mirror;

    /// <summary>
    /// Scale factor for ScaledMirror (e.g. 0.5 = half size).
    /// </summary>
    public decimal ScaleFactor { get; set; } = 1.0m;

    /// <summary>
    /// Optional brokerage account to execute mirrored trades through.
    /// </summary>
    public string? BrokerageAccountId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
}
