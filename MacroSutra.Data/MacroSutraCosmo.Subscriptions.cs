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
}
