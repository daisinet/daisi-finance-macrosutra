using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string StrategyIdPrefix = "str";
    public const string StrategiesContainerName = "Strategies";
    public const string StrategiesPartitionKeyName = nameof(TradingStrategy.AccountId);

    public virtual async Task<TradingStrategy> CreateStrategyAsync(TradingStrategy strategy)
    {
        if (string.IsNullOrEmpty(strategy.id))
            strategy.id = GenerateId(StrategyIdPrefix);
        strategy.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(StrategiesContainerName);
        var response = await container.CreateItemAsync(strategy, new PartitionKey(strategy.AccountId));
        return response.Resource;
    }

    public virtual async Task<TradingStrategy?> GetStrategyAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(StrategiesContainerName);
            var response = await container.ReadItemAsync<TradingStrategy>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<List<TradingStrategy>> GetStrategiesByUserAsync(string accountId, string? userId = null)
    {
        var container = await GetContainerAsync(StrategiesContainerName);

        var sql = "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'TradingStrategy'";
        if (!string.IsNullOrEmpty(userId))
            sql += " AND c.UserId = @userId";

        var query = new QueryDefinition(sql)
            .WithParameter("@accountId", accountId);

        if (!string.IsNullOrEmpty(userId))
            query = query.WithParameter("@userId", userId);

        var results = new List<TradingStrategy>();
        using var iterator = container.GetItemQueryIterator<TradingStrategy>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<TradingStrategy> UpdateStrategyAsync(TradingStrategy strategy)
    {
        strategy.UpdatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(StrategiesContainerName);
        var response = await container.UpsertItemAsync(strategy, new PartitionKey(strategy.AccountId));
        return response.Resource;
    }

    public virtual async Task DeleteStrategyAsync(string id, string accountId)
    {
        var container = await GetContainerAsync(StrategiesContainerName);
        await container.DeleteItemAsync<TradingStrategy>(id, new PartitionKey(accountId));
    }
}
