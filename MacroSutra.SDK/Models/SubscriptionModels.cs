namespace MacroSutra.SDK.Models;

public class Subscription
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string SubscriberUserId { get; set; } = "";
    public string PublisherAccountId { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public string PublisherName { get; set; } = "";
    public string ActionType { get; set; } = "Mirror";
    public decimal ScaleFactor { get; set; } = 1.0m;
    public string? BrokerageAccountId { get; set; }
    public long CreditPrice { get; set; }
    public string? WebhookUrl { get; set; }
    public string? NotificationEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
}

public class SubscriptionAction
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string SubscriptionId { get; set; } = "";
    public string? StrategyId { get; set; }
    public string? TradeId { get; set; }
    public string Symbol { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedUtc { get; set; }
}

public class CreateSubscriptionRequest
{
    public string StrategyId { get; set; } = "";
    public string ActionType { get; set; } = "Mirror";
    public decimal ScaleFactor { get; set; } = 1.0m;
    public string? BrokerageAccountId { get; set; }
    public string? WebhookUrl { get; set; }
    public string? NotificationEmail { get; set; }
}
