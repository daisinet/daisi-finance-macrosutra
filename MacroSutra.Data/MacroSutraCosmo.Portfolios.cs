using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string BrokerageAccountIdPrefix = "bra";
    public const string PositionIdPrefix = "pos";
    public const string PortfoliosContainerName = "Portfolios";
    public const string PortfoliosPartitionKeyName = nameof(BrokerageAccount.AccountId);

    // ── Brokerage Accounts ──

    public virtual async Task<BrokerageAccount> CreateBrokerageAccountAsync(BrokerageAccount account)
    {
        if (string.IsNullOrEmpty(account.id))
            account.id = GenerateId(BrokerageAccountIdPrefix);
        account.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(PortfoliosContainerName);
        var response = await container.CreateItemAsync(account, new PartitionKey(account.AccountId));
        return response.Resource;
    }

    public virtual async Task<BrokerageAccount?> GetBrokerageAccountAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(PortfoliosContainerName);
            var response = await container.ReadItemAsync<BrokerageAccount>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<List<BrokerageAccount>> GetBrokerageAccountsAsync(string accountId, bool activeOnly = false)
    {
        var container = await GetContainerAsync(PortfoliosContainerName);

        var sql = "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'BrokerageAccount'";
        if (activeOnly)
            sql += " AND c.IsActive = true";

        var query = new QueryDefinition(sql)
            .WithParameter("@accountId", accountId);

        var results = new List<BrokerageAccount>();
        using var iterator = container.GetItemQueryIterator<BrokerageAccount>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<BrokerageAccount> UpdateBrokerageAccountAsync(BrokerageAccount account)
    {
        account.UpdatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(PortfoliosContainerName);
        var response = await container.UpsertItemAsync(account, new PartitionKey(account.AccountId));
        return response.Resource;
    }

    // ── Positions ──

    public virtual async Task<Position> CreatePositionAsync(Position position)
    {
        if (string.IsNullOrEmpty(position.id))
            position.id = GenerateId(PositionIdPrefix);
        position.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(PortfoliosContainerName);
        var response = await container.CreateItemAsync(position, new PartitionKey(position.AccountId));
        return response.Resource;
    }

    public virtual async Task<List<Position>> GetPositionsAsync(string accountId, string? brokerageAccountId = null)
    {
        var container = await GetContainerAsync(PortfoliosContainerName);

        var sql = "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'Position'";
        if (!string.IsNullOrEmpty(brokerageAccountId))
            sql += " AND c.BrokerageAccountId = @brokerageAccountId";

        var query = new QueryDefinition(sql)
            .WithParameter("@accountId", accountId);

        if (!string.IsNullOrEmpty(brokerageAccountId))
            query = query.WithParameter("@brokerageAccountId", brokerageAccountId);

        var results = new List<Position>();
        using var iterator = container.GetItemQueryIterator<Position>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<Position> UpdatePositionAsync(Position position)
    {
        position.UpdatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(PortfoliosContainerName);
        var response = await container.UpsertItemAsync(position, new PartitionKey(position.AccountId));
        return response.Resource;
    }

    public virtual async Task DeletePositionAsync(string id, string accountId)
    {
        var container = await GetContainerAsync(PortfoliosContainerName);
        await container.DeleteItemAsync<Position>(id, new PartitionKey(accountId));
    }
}
