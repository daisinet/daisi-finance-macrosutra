using MacroSutra.Core.Models;
using MacroSutra.Data;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Subscription CRUD, cancellation, and community stats refresh.
/// Credit billing is handled at the web/API layer where Daisi SDK is available.
/// </summary>
public class SubscriptionService(
    MacroSutraCosmo cosmo,
    CommunityService communityService,
    ILogger<SubscriptionService> logger)
{
    /// <summary>
    /// Subscribe to a strategy with duplicate detection.
    /// Credit billing should be done by the caller before calling this method.
    /// </summary>
    public virtual async Task<Subscription> SubscribeAsync(Subscription subscription)
    {
        // Check for duplicate active subscription
        var existing = await cosmo.GetActiveSubscriptionAsync(subscription.AccountId, subscription.StrategyId);
        if (existing != null)
            throw new InvalidOperationException("You already have an active subscription to this strategy.");

        var created = await cosmo.CreateSubscriptionAsync(subscription);

        // Refresh community stats (subscriber count)
        try
        {
            await communityService.RefreshCommunityStatsAsync(subscription.StrategyId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh community stats after subscription to {StrategyId}",
                subscription.StrategyId);
        }

        return created;
    }

    /// <summary>
    /// Simple create without duplicate checks (backward compat).
    /// </summary>
    public virtual async Task<Subscription> CreateSubscriptionAsync(Subscription subscription)
    {
        return await cosmo.CreateSubscriptionAsync(subscription);
    }

    public virtual async Task<Subscription?> GetSubscriptionAsync(string id, string accountId)
    {
        return await cosmo.GetSubscriptionAsync(id, accountId);
    }

    public virtual async Task<List<Subscription>> GetSubscriptionsBySubscriberAsync(string accountId)
    {
        return await cosmo.GetSubscriptionsBySubscriberAsync(accountId);
    }

    public virtual async Task<List<Subscription>> GetSubscriptionsByStrategyAsync(string strategyId)
    {
        return await cosmo.GetSubscriptionsByStrategyAsync(strategyId);
    }

    public virtual async Task<List<Subscription>> GetPublisherSubscriptionsAsync(string publisherAccountId)
    {
        return await cosmo.GetSubscriptionsByPublisherAsync(publisherAccountId);
    }

    public virtual async Task<List<SubscriptionAction>> GetSubscriptionActionsAsync(
        string accountId, string? subscriptionId = null)
    {
        return await cosmo.GetSubscriptionActionsAsync(accountId, subscriptionId);
    }

    public virtual async Task<Subscription> CancelSubscriptionAsync(string id, string accountId)
    {
        var subscription = await cosmo.GetSubscriptionAsync(id, accountId)
            ?? throw new InvalidOperationException("Subscription not found.");

        subscription.IsActive = false;
        var updated = await cosmo.UpdateSubscriptionAsync(subscription);

        // Refresh community stats (subscriber count)
        try
        {
            await communityService.RefreshCommunityStatsAsync(subscription.StrategyId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh community stats after cancellation of {SubscriptionId}",
                subscription.id);
        }

        return updated;
    }
}
