using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string TradeIdPrefix = "trd";
    public const string TradesContainerName = "Trades";
    public const string TradesPartitionKeyName = nameof(Trade.AccountId);

    public virtual async Task<Trade> CreateTradeAsync(Trade trade)
    {
        if (string.IsNullOrEmpty(trade.id))
            trade.id = GenerateId(TradeIdPrefix);
        trade.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(TradesContainerName);
        var response = await container.CreateItemAsync(trade, new PartitionKey(trade.AccountId));
        return response.Resource;
    }

    public virtual async Task<Trade?> GetTradeAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(TradesContainerName);
            var response = await container.ReadItemAsync<Trade>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<List<Trade>> GetTradesAsync(string accountId, string? symbol = null, TradeStatus? status = null, string? strategyId = null)
    {
        var container = await GetContainerAsync(TradesContainerName);

        var sql = "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'Trade'";
        if (!string.IsNullOrEmpty(symbol))
            sql += " AND c.Symbol = @symbol";
        if (status.HasValue)
            sql += " AND c.Status = @status";
        if (!string.IsNullOrEmpty(strategyId))
            sql += " AND c.StrategyId = @strategyId";

        sql += " ORDER BY c.CreatedUtc DESC";

        var query = new QueryDefinition(sql)
            .WithParameter("@accountId", accountId);

        if (!string.IsNullOrEmpty(symbol))
            query = query.WithParameter("@symbol", symbol);
        if (status.HasValue)
            query = query.WithParameter("@status", (int)status.Value);
        if (!string.IsNullOrEmpty(strategyId))
            query = query.WithParameter("@strategyId", strategyId);

        var results = new List<Trade>();
        using var iterator = container.GetItemQueryIterator<Trade>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<Trade> UpdateTradeAsync(Trade trade)
    {
        trade.UpdatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(TradesContainerName);
        var response = await container.UpsertItemAsync(trade, new PartitionKey(trade.AccountId));
        return response.Resource;
    }
}
