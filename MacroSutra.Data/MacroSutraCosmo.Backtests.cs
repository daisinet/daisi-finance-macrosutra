using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string BacktestIdPrefix = "bt";
    public const string BacktestsContainerName = "Backtests";
    public const string BacktestsPartitionKeyName = nameof(BacktestResult.AccountId);

    public virtual async Task<BacktestResult> CreateBacktestAsync(BacktestResult result)
    {
        if (string.IsNullOrEmpty(result.id))
            result.id = GenerateId(BacktestIdPrefix);
        result.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(BacktestsContainerName);
        var response = await container.CreateItemAsync(result, new PartitionKey(result.AccountId));
        return response.Resource;
    }

    public virtual async Task<BacktestResult?> GetBacktestAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(BacktestsContainerName);
            var response = await container.ReadItemAsync<BacktestResult>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<List<BacktestResult>> GetBacktestsAsync(string accountId, string? strategyId = null)
    {
        var container = await GetContainerAsync(BacktestsContainerName);

        var sql = "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'BacktestResult'";
        if (!string.IsNullOrEmpty(strategyId))
            sql += " AND c.StrategyId = @strategyId";
        sql += " ORDER BY c.CreatedUtc DESC";

        var query = new QueryDefinition(sql)
            .WithParameter("@accountId", accountId);

        if (!string.IsNullOrEmpty(strategyId))
            query = query.WithParameter("@strategyId", strategyId);

        var results = new List<BacktestResult>();
        using var iterator = container.GetItemQueryIterator<BacktestResult>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<BacktestResult> UpdateBacktestAsync(BacktestResult result)
    {
        var container = await GetContainerAsync(BacktestsContainerName);
        var response = await container.UpsertItemAsync(result, new PartitionKey(result.AccountId));
        return response.Resource;
    }

    public virtual async Task DeleteBacktestAsync(string id, string accountId)
    {
        var container = await GetContainerAsync(BacktestsContainerName);
        await container.DeleteItemAsync<BacktestResult>(id, new PartitionKey(accountId));
    }
}
