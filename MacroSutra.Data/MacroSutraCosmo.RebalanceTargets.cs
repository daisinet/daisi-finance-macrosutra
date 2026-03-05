using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    internal const string RebalanceTargetsContainerName = "RebalanceTargets";
    internal const string RebalanceTargetsPartitionKeyName = "AccountId";
    private const string RebalanceTargetIdPrefix = "rbt";

    public virtual async Task<RebalanceTarget> CreateRebalanceTargetAsync(RebalanceTarget target)
    {
        target.id = GenerateId(RebalanceTargetIdPrefix);
        target.CreatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(RebalanceTargetsContainerName);
        var response = await container.CreateItemAsync(target, new PartitionKey(target.AccountId));
        return response.Resource;
    }

    public virtual async Task<RebalanceTarget?> GetRebalanceTargetAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(RebalanceTargetsContainerName);
            var response = await container.ReadItemAsync<RebalanceTarget>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<List<RebalanceTarget>> GetRebalanceTargetsAsync(string accountId)
    {
        var container = await GetContainerAsync(RebalanceTargetsContainerName);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.AccountId = @accountId ORDER BY c.CreatedUtc DESC")
            .WithParameter("@accountId", accountId);

        var results = new List<RebalanceTarget>();
        using var iterator = container.GetItemQueryIterator<RebalanceTarget>(query);
        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            results.AddRange(batch);
        }
        return results;
    }

    public virtual async Task<RebalanceTarget> UpdateRebalanceTargetAsync(RebalanceTarget target)
    {
        var container = await GetContainerAsync(RebalanceTargetsContainerName);
        var response = await container.UpsertItemAsync(target, new PartitionKey(target.AccountId));
        return response.Resource;
    }

    public virtual async Task DeleteRebalanceTargetAsync(string id, string accountId)
    {
        var container = await GetContainerAsync(RebalanceTargetsContainerName);
        await container.DeleteItemAsync<RebalanceTarget>(id, new PartitionKey(accountId));
    }
}
