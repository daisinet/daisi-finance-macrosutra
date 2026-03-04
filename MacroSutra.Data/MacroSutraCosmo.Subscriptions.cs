using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string SubscriptionIdPrefix = "sub";
    public const string SubscriptionsContainerName = "Subscriptions";
    public const string SubscriptionsPartitionKeyName = nameof(Subscription.AccountId);

    public virtual async Task<Subscription> CreateSubscriptionAsync(Subscription subscription)
    {
        if (string.IsNullOrEmpty(subscription.id))
            subscription.id = GenerateId(SubscriptionIdPrefix);
        subscription.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(SubscriptionsContainerName);
        var response = await container.CreateItemAsync(subscription, new PartitionKey(subscription.AccountId));
        return response.Resource;
    }

    public virtual async Task<Subscription?> GetSubscriptionAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(SubscriptionsContainerName);
            var response = await container.ReadItemAsync<Subscription>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<List<Subscription>> GetSubscriptionsBySubscriberAsync(string accountId)
    {
        var container = await GetContainerAsync(SubscriptionsContainerName);
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'Subscription'")
            .WithParameter("@accountId", accountId);

        var results = new List<Subscription>();
        using var iterator = container.GetItemQueryIterator<Subscription>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<List<Subscription>> GetSubscriptionsByStrategyAsync(string strategyId)
    {
        var container = await GetContainerAsync(SubscriptionsContainerName);
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.StrategyId = @strategyId AND c.Type = 'Subscription'")
            .WithParameter("@strategyId", strategyId);

        var results = new List<Subscription>();
        using var iterator = container.GetItemQueryIterator<Subscription>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<Subscription> UpdateSubscriptionAsync(Subscription subscription)
    {
        subscription.UpdatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(SubscriptionsContainerName);
        var response = await container.UpsertItemAsync(subscription, new PartitionKey(subscription.AccountId));
        return response.Resource;
    }

    public virtual async Task DeleteSubscriptionAsync(string id, string accountId)
    {
        var container = await GetContainerAsync(SubscriptionsContainerName);
        await container.DeleteItemAsync<Subscription>(id, new PartitionKey(accountId));
    }

    /// <summary>
    /// Persist a subscription action record (same container, same partition as subscription).
    /// </summary>
    public virtual async Task<SubscriptionAction> CreateSubscriptionActionAsync(SubscriptionAction action)
    {
        if (string.IsNullOrEmpty(action.id))
            action.id = GenerateId("sa");
        action.ExecutedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(SubscriptionsContainerName);
        var response = await container.CreateItemAsync(action, new PartitionKey(action.AccountId));
        return response.Resource;
    }

    /// <summary>
    /// Get subscription actions for an account, optionally filtered by subscription, ordered newest first.
    /// </summary>
    public virtual async Task<List<SubscriptionAction>> GetSubscriptionActionsAsync(
        string accountId, string? subscriptionId = null, int limit = 50)
    {
        var container = await GetContainerAsync(SubscriptionsContainerName);
        var sql = "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'SubscriptionAction'";
        if (!string.IsNullOrEmpty(subscriptionId))
            sql += " AND c.SubscriptionId = @subscriptionId";
        sql += " ORDER BY c.ExecutedUtc DESC OFFSET 0 LIMIT @limit";

        var query = new QueryDefinition(sql)
            .WithParameter("@accountId", accountId)
            .WithParameter("@limit", limit);
        if (!string.IsNullOrEmpty(subscriptionId))
            query = query.WithParameter("@subscriptionId", subscriptionId);

        var results = new List<SubscriptionAction>();
        using var iterator = container.GetItemQueryIterator<SubscriptionAction>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    /// <summary>
    /// Get all active subscriptions where the current user is the publisher (cross-partition).
    /// </summary>
    public virtual async Task<List<Subscription>> GetSubscriptionsByPublisherAsync(string publisherAccountId)
    {
        var container = await GetContainerAsync(SubscriptionsContainerName);
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.PublisherAccountId = @publisherAccountId AND c.Type = 'Subscription' AND c.IsActive = true")
            .WithParameter("@publisherAccountId", publisherAccountId);

        var results = new List<Subscription>();
        using var iterator = container.GetItemQueryIterator<Subscription>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = -1 });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    /// <summary>
    /// Check if a subscriber already has an active subscription to a strategy (duplicate detection).
    /// </summary>
    public virtual async Task<Subscription?> GetActiveSubscriptionAsync(string subscriberAccountId, string strategyId)
    {
        var container = await GetContainerAsync(SubscriptionsContainerName);
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.AccountId = @accountId AND c.StrategyId = @strategyId AND c.Type = 'Subscription' AND c.IsActive = true")
            .WithParameter("@accountId", subscriberAccountId)
            .WithParameter("@strategyId", strategyId);

        using var iterator = container.GetItemQueryIterator<Subscription>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }
}
