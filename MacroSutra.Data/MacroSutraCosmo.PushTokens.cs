using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string PushTokenIdPrefix = "ptk";
    public const string PushTokensContainerName = "PushTokens";
    public const string PushTokensPartitionKeyName = nameof(PushToken.AccountId);

    public virtual async Task<PushToken> CreatePushTokenAsync(PushToken token)
    {
        if (string.IsNullOrEmpty(token.id))
            token.id = GenerateId(PushTokenIdPrefix);
        token.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(PushTokensContainerName);
        var response = await container.CreateItemAsync(token, new PartitionKey(token.AccountId));
        return response.Resource;
    }

    public virtual async Task<List<PushToken>> GetPushTokensAsync(string accountId)
    {
        var container = await GetContainerAsync(PushTokensContainerName);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.AccountId = @accountId AND c.IsActive = true")
            .WithParameter("@accountId", accountId);

        var results = new List<PushToken>();
        using var iterator = container.GetItemQueryIterator<PushToken>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(accountId)
        });
        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            results.AddRange(batch);
        }
        return results;
    }

    public virtual async Task<List<PushToken>> GetPushTokensByUserAsync(string userId)
    {
        var container = await GetContainerAsync(PushTokensContainerName);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId AND c.IsActive = true")
            .WithParameter("@userId", userId);

        var results = new List<PushToken>();
        using var iterator = container.GetItemQueryIterator<PushToken>(query);
        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            results.AddRange(batch);
        }
        return results;
    }

    public virtual async Task<PushToken> UpdatePushTokenAsync(PushToken token)
    {
        var container = await GetContainerAsync(PushTokensContainerName);
        var response = await container.ReplaceItemAsync(token, token.id, new PartitionKey(token.AccountId));
        return response.Resource;
    }

    public virtual async Task DeletePushTokenAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(PushTokensContainerName);
            await container.DeleteItemAsync<PushToken>(id, new PartitionKey(accountId));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already deleted — safe to ignore
        }
    }
}
