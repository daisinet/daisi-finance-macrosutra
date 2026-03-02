using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// Subscription CRUD and cancellation.
/// </summary>
public class SubscriptionService(MacroSutraCosmo cosmo)
{
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

    public virtual async Task<Subscription> CancelSubscriptionAsync(string id, string accountId)
    {
        var subscription = await cosmo.GetSubscriptionAsync(id, accountId)
            ?? throw new InvalidOperationException("Subscription not found.");

        subscription.IsActive = false;
        return await cosmo.UpdateSubscriptionAsync(subscription);
    }
}
