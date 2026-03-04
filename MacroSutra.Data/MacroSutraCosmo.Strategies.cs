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

    /// <summary>
    /// Gets all active strategies across all accounts (cross-partition query).
    /// Used by the StrategyEvaluationService background service.
    /// </summary>
    public virtual async Task<List<TradingStrategy>> GetAllActiveStrategiesAsync()
    {
        var container = await GetContainerAsync(StrategiesContainerName);

        var sql = "SELECT * FROM c WHERE c.Type = 'TradingStrategy' AND c.IsActive = true";
        var query = new QueryDefinition(sql);

        var options = new QueryRequestOptions { MaxConcurrency = -1 };
        var results = new List<TradingStrategy>();
        using var iterator = container.GetItemQueryIterator<TradingStrategy>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    /// <summary>
    /// Gets public strategies across all accounts with pagination (cross-partition query).
    /// </summary>
    public virtual async Task<List<TradingStrategy>> GetPublicStrategiesAsync(int offset = 0, int limit = 20, string? sortBy = null)
    {
        var container = await GetContainerAsync(StrategiesContainerName);

        var orderClause = sortBy?.ToLowerInvariant() switch
        {
            "name" => "ORDER BY c.Name",
            _ => "ORDER BY c.CreatedUtc DESC"
        };

        var sql = $"SELECT * FROM c WHERE c.Type = 'TradingStrategy' AND c.Visibility = 'Public' {orderClause} OFFSET @offset LIMIT @limit";
        var query = new QueryDefinition(sql)
            .WithParameter("@offset", offset)
            .WithParameter("@limit", limit);

        var options = new QueryRequestOptions { MaxConcurrency = -1 };
        var results = new List<TradingStrategy>();
        using var iterator = container.GetItemQueryIterator<TradingStrategy>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    /// <summary>
    /// Gets a single public strategy by id (cross-partition — no accountId needed).
    /// </summary>
    public virtual async Task<TradingStrategy?> GetPublicStrategyAsync(string id)
    {
        var container = await GetContainerAsync(StrategiesContainerName);

        var sql = "SELECT * FROM c WHERE c.id = @id AND c.Type = 'TradingStrategy' AND c.Visibility = 'Public'";
        var query = new QueryDefinition(sql)
            .WithParameter("@id", id);

        var options = new QueryRequestOptions { MaxConcurrency = -1 };
        using var iterator = container.GetItemQueryIterator<TradingStrategy>(query, requestOptions: options);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    /// <summary>
    /// Gets all completed backtests for a strategy across all accounts (cross-partition).
    /// Used by the leaderboard to find best backtest for public strategies.
    /// </summary>
    public virtual async Task<List<BacktestResult>> GetBacktestsByStrategyIdAsync(string strategyId)
    {
        var container = await GetContainerAsync(BacktestsContainerName);

        var sql = "SELECT * FROM c WHERE c.StrategyId = @strategyId AND c.Type = 'BacktestResult' AND c.Status = 'Completed' ORDER BY c.CreatedUtc DESC";
        var query = new QueryDefinition(sql)
            .WithParameter("@strategyId", strategyId);

        var options = new QueryRequestOptions { MaxConcurrency = -1 };
        var results = new List<BacktestResult>();
        using var iterator = container.GetItemQueryIterator<BacktestResult>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }
}
