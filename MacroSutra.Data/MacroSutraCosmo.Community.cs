using MacroSutra.Core.Models;
using Microsoft.Azure.Cosmos;

namespace MacroSutra.Data;

public partial class MacroSutraCosmo
{
    public const string ReviewIdPrefix = "rev";
    public const string CommunityContainerName = "Community";
    public const string CommunityPartitionKeyName = nameof(StrategyReview.StrategyId);

    public virtual async Task<StrategyReview> CreateReviewAsync(StrategyReview review)
    {
        if (string.IsNullOrEmpty(review.id))
            review.id = GenerateId(ReviewIdPrefix);
        review.CreatedUtc = DateTime.UtcNow;

        var container = await GetContainerAsync(CommunityContainerName);
        var response = await container.CreateItemAsync(review, new PartitionKey(review.StrategyId));
        return response.Resource;
    }

    public virtual async Task<List<StrategyReview>> GetReviewsAsync(string strategyId)
    {
        var container = await GetContainerAsync(CommunityContainerName);
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.StrategyId = @strategyId AND c.Type = 'StrategyReview' ORDER BY c.CreatedUtc DESC")
            .WithParameter("@strategyId", strategyId);

        var results = new List<StrategyReview>();
        using var iterator = container.GetItemQueryIterator<StrategyReview>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public virtual async Task<StrategyReview?> GetReviewByUserAsync(string strategyId, string reviewerAccountId)
    {
        var container = await GetContainerAsync(CommunityContainerName);
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.StrategyId = @strategyId AND c.ReviewerAccountId = @reviewerAccountId AND c.Type = 'StrategyReview'")
            .WithParameter("@strategyId", strategyId)
            .WithParameter("@reviewerAccountId", reviewerAccountId);

        using var iterator = container.GetItemQueryIterator<StrategyReview>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public virtual async Task<StrategyReview> UpdateReviewAsync(StrategyReview review)
    {
        review.UpdatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(CommunityContainerName);
        var response = await container.UpsertItemAsync(review, new PartitionKey(review.StrategyId));
        return response.Resource;
    }

    public virtual async Task DeleteReviewAsync(string id, string strategyId)
    {
        var container = await GetContainerAsync(CommunityContainerName);
        await container.DeleteItemAsync<StrategyReview>(id, new PartitionKey(strategyId));
    }

    public virtual async Task<StrategyCommunityStats?> GetCommunityStatsAsync(string strategyId)
    {
        try
        {
            var container = await GetContainerAsync(CommunityContainerName);
            var response = await container.ReadItemAsync<StrategyCommunityStats>(strategyId, new PartitionKey(strategyId));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<StrategyCommunityStats> UpsertCommunityStatsAsync(StrategyCommunityStats stats)
    {
        stats.LastUpdatedUtc = DateTime.UtcNow;
        var container = await GetContainerAsync(CommunityContainerName);
        var response = await container.UpsertItemAsync(stats, new PartitionKey(stats.StrategyId));
        return response.Resource;
    }
}
