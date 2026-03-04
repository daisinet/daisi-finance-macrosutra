using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string TriggerRecordIdPrefix = "trig";
    public const string StrategyPerformanceContainerName = "StrategyPerformance";
    public const string StrategyPerformancePartitionKeyName = nameof(StrategyTriggerRecord.AccountId);

    public virtual async Task<StrategyTriggerRecord> CreateTriggerRecordAsync(StrategyTriggerRecord record)
    {
        if (string.IsNullOrEmpty(record.id))
            record.id = GenerateId(TriggerRecordIdPrefix);

        var container = await GetContainerAsync(StrategyPerformanceContainerName);
        var response = await container.CreateItemAsync(record, new PartitionKey(record.AccountId));
        return response.Resource;
    }

    public virtual async Task<StrategyTriggerRecord?> GetTriggerRecordAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(StrategyPerformanceContainerName);
            var response = await container.ReadItemAsync<StrategyTriggerRecord>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<List<StrategyTriggerRecord>> GetTriggerRecordsAsync(string accountId, string? strategyId = null)
    {
        var container = await GetContainerAsync(StrategyPerformanceContainerName);

        var sql = "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'StrategyTriggerRecord'";
        if (!string.IsNullOrEmpty(strategyId))
            sql += " AND c.StrategyId = @strategyId";
        sql += " ORDER BY c.TriggeredUtc DESC";

        var query = new QueryDefinition(sql)
            .WithParameter("@accountId", accountId);
        if (!string.IsNullOrEmpty(strategyId))
            query = query.WithParameter("@strategyId", strategyId);

        var results = new List<StrategyTriggerRecord>();
        using var iterator = container.GetItemQueryIterator<StrategyTriggerRecord>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<StrategyTriggerRecord> UpdateTriggerRecordAsync(StrategyTriggerRecord record)
    {
        var container = await GetContainerAsync(StrategyPerformanceContainerName);
        var response = await container.UpsertItemAsync(record, new PartitionKey(record.AccountId));
        return response.Resource;
    }

    /// <summary>
    /// Finds open trigger records for a given trade ID (cross-partition).
    /// Used by OrderStatusTracker to update outcomes when trades fill.
    /// </summary>
    public virtual async Task<List<StrategyTriggerRecord>> GetOpenTriggerRecordsByTradeIdAsync(string tradeId)
    {
        var container = await GetContainerAsync(StrategyPerformanceContainerName);

        var sql = "SELECT * FROM c WHERE c.Type = 'StrategyTriggerRecord' AND c.Outcome = 0 AND ARRAY_CONTAINS(c.TradeIds, @tradeId)";
        var query = new QueryDefinition(sql)
            .WithParameter("@tradeId", tradeId);

        var options = new QueryRequestOptions { MaxConcurrency = -1 };
        var results = new List<StrategyTriggerRecord>();
        using var iterator = container.GetItemQueryIterator<StrategyTriggerRecord>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }
}
