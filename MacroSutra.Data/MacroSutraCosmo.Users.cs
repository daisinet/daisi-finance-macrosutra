using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string UserIdPrefix = "msu";
    public const string UsersContainerName = "Users";
    public const string UsersPartitionKeyName = nameof(MacroSutraUser.AccountId);

    public virtual async Task<MacroSutraUser> CreateUserAsync(MacroSutraUser user)
    {
        if (string.IsNullOrEmpty(user.id))
            user.id = GenerateId(UserIdPrefix);
        user.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(UsersContainerName);
        var response = await container.CreateItemAsync(user, new PartitionKey(user.AccountId));
        return response.Resource;
    }

    public virtual async Task<MacroSutraUser?> GetUserAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(UsersContainerName);
            var response = await container.ReadItemAsync<MacroSutraUser>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<MacroSutraUser?> GetUserByDaisinetIdAsync(string daisinetUserId, string accountId)
    {
        var container = await GetContainerAsync(UsersContainerName);
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'MacroSutraUser' AND c.DaisinetUserId = @daisinetUserId")
            .WithParameter("@accountId", accountId)
            .WithParameter("@daisinetUserId", daisinetUserId);

        using var iterator = container.GetItemQueryIterator<MacroSutraUser>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public virtual async Task<List<MacroSutraUser>> GetUsersAsync(string accountId, bool activeOnly = false)
    {
        var container = await GetContainerAsync(UsersContainerName);

        var sql = "SELECT * FROM c WHERE c.AccountId = @accountId AND c.Type = 'MacroSutraUser'";
        if (activeOnly)
            sql += " AND c.IsActive = true";

        var query = new QueryDefinition(sql)
            .WithParameter("@accountId", accountId);

        var results = new List<MacroSutraUser>();
        using var iterator = container.GetItemQueryIterator<MacroSutraUser>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<MacroSutraUser> UpdateUserAsync(MacroSutraUser user)
    {
        user.UpdatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(UsersContainerName);
        var response = await container.UpsertItemAsync(user, new PartitionKey(user.AccountId));
        return response.Resource;
    }
}
