using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Fans out trades from a publisher's strategy to all active subscribers.
/// Each subscriber dispatch is isolated — one failure does not block others.
/// </summary>
public class SubscriptionDispatchService(
    MacroSutraCosmo cosmo,
    BrokerageProviderFactory providerFactory,
    PortfolioService portfolioService,
    TradeService tradeService,
    EmailNotificationService emailService,
    WebhookDispatchService webhookService,
    PushNotificationService pushService,
    ILogger<SubscriptionDispatchService> logger)
{
    /// <summary>
    /// Dispatch all trades from a triggered strategy to active subscribers.
    /// </summary>
    public virtual async Task DispatchAsync(TradingStrategy strategy, List<Trade> trades)
    {
        if (trades.Count == 0) return;

        List<Subscription> subscriptions;
        try
        {
            subscriptions = await cosmo.GetSubscriptionsByStrategyAsync(strategy.id);
            subscriptions = subscriptions.Where(s => s.IsActive).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch subscriptions for strategy {StrategyId}", strategy.id);
            return;
        }

        if (subscriptions.Count == 0) return;

        logger.LogInformation("Dispatching {TradeCount} trades to {SubCount} subscribers for strategy {StrategyId}",
            trades.Count, subscriptions.Count, strategy.id);

        foreach (var subscription in subscriptions)
        {
            foreach (var trade in trades)
            {
                try
                {
                    await DispatchToSubscriberAsync(subscription, trade, strategy);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed dispatching to subscription {SubscriptionId} for trade {TradeId}",
                        subscription.id, trade.id);

                    // Record failed action
                    try
                    {
                        await cosmo.CreateSubscriptionActionAsync(new SubscriptionAction
                        {
                            AccountId = subscription.AccountId,
                            SubscriptionId = subscription.id,
                            StrategyId = strategy.id,
                            ActionType = subscription.ActionType,
                            Symbol = trade.Symbol,
                            Side = trade.Side,
                            Quantity = trade.Quantity,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                    catch { /* best-effort persistence */ }
                }
            }
        }
    }

    private async Task DispatchToSubscriberAsync(Subscription subscription, Trade trade, TradingStrategy strategy)
    {
        switch (subscription.ActionType)
        {
            case SubscriptionActionType.Mirror:
            case SubscriptionActionType.ScaledMirror:
                await DispatchMirrorAsync(subscription, trade, strategy);
                break;

            case SubscriptionActionType.Alert:
            case SubscriptionActionType.Email:
                await DispatchEmailAsync(subscription, trade, strategy);
                break;

            case SubscriptionActionType.Webhook:
                await DispatchWebhookAsync(subscription, trade, strategy);
                break;

            case SubscriptionActionType.Push:
                await DispatchPushAsync(subscription, trade, strategy);
                break;

            default:
                logger.LogWarning("Unknown action type {ActionType} for subscription {SubscriptionId}",
                    subscription.ActionType, subscription.id);
                break;
        }
    }

    private async Task DispatchMirrorAsync(Subscription subscription, Trade trade, TradingStrategy strategy)
    {
        if (string.IsNullOrEmpty(subscription.BrokerageAccountId))
        {
            await RecordActionAsync(subscription, trade, strategy, false,
                errorMessage: "No brokerage account configured for mirror subscription");
            return;
        }

        var brokerageAccount = await portfolioService.GetBrokerageAccountAsync(
            subscription.BrokerageAccountId, subscription.AccountId);
        if (brokerageAccount == null)
        {
            await RecordActionAsync(subscription, trade, strategy, false,
                errorMessage: "Brokerage account not found");
            return;
        }

        // Scale quantity
        var quantity = trade.Quantity;
        if (subscription.ActionType == SubscriptionActionType.ScaledMirror)
            quantity = Math.Round(quantity * subscription.ScaleFactor, 2);

        if (quantity <= 0)
        {
            await RecordActionAsync(subscription, trade, strategy, false,
                errorMessage: "Scaled quantity is zero or negative");
            return;
        }

        // Record the mirrored trade in subscriber's account
        var mirroredTrade = new Trade
        {
            AccountId = subscription.AccountId,
            UserId = subscription.SubscriberUserId,
            StrategyId = strategy.id,
            BrokerageAccountId = subscription.BrokerageAccountId,
            Symbol = trade.Symbol,
            Side = trade.Side,
            OrderType = trade.OrderType,
            Quantity = quantity,
            LimitPrice = trade.LimitPrice,
            StopPrice = trade.StopPrice,
            Status = TradeStatus.Pending,
            Notes = $"Mirrored from subscription {subscription.id}"
        };
        mirroredTrade = await tradeService.RecordTradeAsync(mirroredTrade);

        // Place the order via brokerage
        try
        {
            var provider = providerFactory.GetProvider(brokerageAccount.Provider);
            var externalOrderId = await provider.PlaceOrderAsync(brokerageAccount.CredentialData, mirroredTrade);
            mirroredTrade.ExternalOrderId = externalOrderId;
            mirroredTrade.Status = TradeStatus.Submitted;
            await tradeService.UpdateTradeStatusAsync(mirroredTrade.id, mirroredTrade.AccountId, TradeStatus.Submitted);

            await RecordActionAsync(subscription, trade, strategy, true, mirroredTrade.id, quantity: quantity);
        }
        catch (Exception ex)
        {
            mirroredTrade.Status = TradeStatus.Failed;
            mirroredTrade.Notes = $"Mirror order failed: {ex.Message}";
            await tradeService.UpdateTradeStatusAsync(mirroredTrade.id, mirroredTrade.AccountId, TradeStatus.Failed);

            await RecordActionAsync(subscription, trade, strategy, false, mirroredTrade.id,
                errorMessage: ex.Message, quantity: quantity);
        }
    }

    private async Task DispatchEmailAsync(Subscription subscription, Trade trade, TradingStrategy strategy)
    {
        var email = subscription.NotificationEmail;
        if (string.IsNullOrEmpty(email))
        {
            await RecordActionAsync(subscription, trade, strategy, false,
                errorMessage: "No notification email configured");
            return;
        }

        var success = await emailService.SendTradeAlertAsync(email, "", trade, strategy);
        await RecordActionAsync(subscription, trade, strategy, success,
            errorMessage: success ? null : "Email send failed");
    }

    private async Task DispatchPushAsync(Subscription subscription, Trade trade, TradingStrategy strategy)
    {
        var success = await pushService.SendTradeAlertAsync(subscription.AccountId, trade, strategy);
        await RecordActionAsync(subscription, trade, strategy, success,
            errorMessage: success ? null : "Push notification send failed or no registered devices");
    }

    private async Task DispatchWebhookAsync(Subscription subscription, Trade trade, TradingStrategy strategy)
    {
        var url = subscription.WebhookUrl;
        if (string.IsNullOrEmpty(url))
        {
            await RecordActionAsync(subscription, trade, strategy, false,
                errorMessage: "No webhook URL configured");
            return;
        }

        var (success, statusCode) = await webhookService.DispatchAsync(url, trade, strategy);
        await RecordActionAsync(subscription, trade, strategy, success,
            errorMessage: success ? null : $"Webhook returned {statusCode}",
            webhookStatusCode: statusCode, webhookUrl: url);
    }

    private async Task RecordActionAsync(
        Subscription subscription, Trade trade, TradingStrategy strategy,
        bool success, string? tradeId = null, string? errorMessage = null,
        int? webhookStatusCode = null, string? webhookUrl = null, decimal? quantity = null)
    {
        var action = new SubscriptionAction
        {
            AccountId = subscription.AccountId,
            SubscriptionId = subscription.id,
            StrategyId = strategy.id,
            TradeId = tradeId ?? trade.id,
            ActionType = subscription.ActionType,
            Symbol = trade.Symbol,
            Side = trade.Side,
            Quantity = quantity ?? trade.Quantity,
            Success = success,
            ErrorMessage = errorMessage,
            WebhookStatusCode = webhookStatusCode,
            WebhookUrl = webhookUrl
        };

        try
        {
            await cosmo.CreateSubscriptionActionAsync(action);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist subscription action for {SubscriptionId}", subscription.id);
        }
    }
}
