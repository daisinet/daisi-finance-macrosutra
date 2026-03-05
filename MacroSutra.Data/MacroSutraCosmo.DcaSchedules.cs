using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    internal const string DcaSchedulesContainerName = "DcaSchedules";
    internal const string DcaSchedulesPartitionKeyName = "AccountId";
    private const string DcaScheduleIdPrefix = "dca";

    public virtual async Task<DcaSchedule> CreateDcaScheduleAsync(DcaSchedule schedule)
    {
        schedule.id = GenerateId(DcaScheduleIdPrefix);
        schedule.CreatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(DcaSchedulesContainerName);
        var response = await container.CreateItemAsync(schedule, new PartitionKey(schedule.AccountId));
        return response.Resource;
    }

    public virtual async Task<DcaSchedule?> GetDcaScheduleAsync(string id, string accountId)
    {
        try
        {
            var container = await GetContainerAsync(DcaSchedulesContainerName);
            var response = await container.ReadItemAsync<DcaSchedule>(id, new PartitionKey(accountId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<List<DcaSchedule>> GetDcaSchedulesAsync(string accountId)
    {
        var container = await GetContainerAsync(DcaSchedulesContainerName);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.AccountId = @accountId ORDER BY c.CreatedUtc DESC")
            .WithParameter("@accountId", accountId);

        var results = new List<DcaSchedule>();
        using var iterator = container.GetItemQueryIterator<DcaSchedule>(query);
        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            results.AddRange(batch);
        }
        return results;
    }

    public virtual async Task<DcaSchedule> UpdateDcaScheduleAsync(DcaSchedule schedule)
    {
        var container = await GetContainerAsync(DcaSchedulesContainerName);
        var response = await container.UpsertItemAsync(schedule, new PartitionKey(schedule.AccountId));
        return response.Resource;
    }

    public virtual async Task DeleteDcaScheduleAsync(string id, string accountId)
    {
        var container = await GetContainerAsync(DcaSchedulesContainerName);
        await container.DeleteItemAsync<DcaSchedule>(id, new PartitionKey(accountId));
    }

    public virtual async Task<List<DcaSchedule>> GetActiveDcaSchedulesDueAsync()
    {
        var container = await GetContainerAsync(DcaSchedulesContainerName);
        var now = DateTime.UtcNow;
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.IsActive = true AND (c.NextExecutionUtc = null OR c.NextExecutionUtc <= @now)")
            .WithParameter("@now", now);

        var results = new List<DcaSchedule>();
        using var iterator = container.GetItemQueryIterator<DcaSchedule>(query);
        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            results.AddRange(batch);
        }
        return results;
    }
}
