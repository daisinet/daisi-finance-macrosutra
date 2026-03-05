using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using Microsoft.Extensions.Caching.Memory;

namespace MacroSutra.Services;

/// <summary>
/// Community features: marketplace browsing, strategy forking, reviews, and leaderboard.
/// </summary>
public class CommunityService(
    MacroSutraCosmo cosmo,
    StrategyService strategyService,
    IMemoryCache cache)
{
    private const string LeaderboardCachePrefix = "leaderboard:";
    private static readonly TimeSpan LeaderboardCacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets paginated public strategies.
    /// </summary>
    public virtual async Task<List<TradingStrategy>> GetPublicStrategiesAsync(int page = 0, int pageSize = 20, string? sortBy = null)
    {
        var offset = page * pageSize;
        return await cosmo.GetPublicStrategiesAsync(offset, pageSize, sortBy);
    }

    /// <summary>
    /// Gets a public strategy by id, along with community stats and recent reviews.
    /// </summary>
    public virtual async Task<TradingStrategy?> GetPublicStrategyAsync(string strategyId)
    {
        return await cosmo.GetPublicStrategyAsync(strategyId);
    }

    /// <summary>
    /// Gets community stats for a strategy.
    /// </summary>
    public virtual async Task<StrategyCommunityStats?> GetCommunityStatsAsync(string strategyId)
    {
        return await cosmo.GetCommunityStatsAsync(strategyId);
    }

    /// <summary>
    /// Gets all reviews for a strategy.
    /// </summary>
    public virtual async Task<List<StrategyReview>> GetReviewsAsync(string strategyId)
    {
        return await cosmo.GetReviewsAsync(strategyId);
    }

    /// <summary>
    /// Fork a public strategy into the target user's account.
    /// </summary>
    public virtual async Task<TradingStrategy> ForkStrategyAsync(string strategyId, string targetAccountId, string targetUserId)
    {
        var source = await cosmo.GetPublicStrategyAsync(strategyId)
            ?? throw new InvalidOperationException("Strategy not found or is not public.");

        var fork = new TradingStrategy
        {
            AccountId = targetAccountId,
            UserId = targetUserId,
            Name = $"{source.Name} (Fork)",
            Description = source.Description,
            Symbols = new List<string>(source.Symbols),
            TriggerGroups = source.TriggerGroups.Select(tg => new TriggerGroup
            {
                Name = tg.Name, Interval = tg.Interval,
                Conditions = CloneConditionGroup(tg.Conditions),
                Actions = tg.Actions.Select(a => new TradeAction
                {
                    ActionId = Guid.NewGuid().ToString("N")[..8],
                    ActionType = a.ActionType,
                    Side = a.Side,
                    Quantity = a.Quantity,
                    QuantityType = a.QuantityType,
                    LimitPrice = a.LimitPrice,
                    StopPrice = a.StopPrice
                }).ToList()
            }).ToList(),
            SizingMode = source.SizingMode,
            Visibility = StrategyVisibility.Private,
            IsActive = false,
            ForkedFromStrategyId = source.id,
            ForkedFromAccountId = source.AccountId
        };

        var created = await strategyService.CreateStrategyAsync(fork);

        // Increment fork count in community stats
        await IncrementForkCountAsync(strategyId);

        return created;
    }

    /// <summary>
    /// Create a review for a public strategy. One review per account per strategy.
    /// </summary>
    public virtual async Task<StrategyReview> CreateReviewAsync(
        string strategyId, string accountId, string userId, string userName, int rating, string? text)
    {
        if (rating < 1 || rating > 5)
            throw new InvalidOperationException("Rating must be between 1 and 5.");

        var existing = await cosmo.GetReviewByUserAsync(strategyId, accountId);
        if (existing != null)
            throw new InvalidOperationException("You have already reviewed this strategy.");

        var review = new StrategyReview
        {
            StrategyId = strategyId,
            ReviewerAccountId = accountId,
            ReviewerUserId = userId,
            ReviewerName = userName,
            Rating = rating,
            ReviewText = text
        };

        var created = await cosmo.CreateReviewAsync(review);
        await RefreshCommunityStatsAsync(strategyId);
        return created;
    }

    /// <summary>
    /// Update an existing review.
    /// </summary>
    public virtual async Task<StrategyReview> UpdateReviewAsync(
        string reviewId, string strategyId, string accountId, int rating, string? text)
    {
        if (rating < 1 || rating > 5)
            throw new InvalidOperationException("Rating must be between 1 and 5.");

        var reviews = await cosmo.GetReviewsAsync(strategyId);
        var review = reviews.FirstOrDefault(r => r.id == reviewId && r.ReviewerAccountId == accountId)
            ?? throw new InvalidOperationException("Review not found or you are not the author.");

        review.Rating = rating;
        review.ReviewText = text;

        var updated = await cosmo.UpdateReviewAsync(review);
        await RefreshCommunityStatsAsync(strategyId);
        return updated;
    }

    /// <summary>
    /// Delete a review.
    /// </summary>
    public virtual async Task DeleteReviewAsync(string reviewId, string strategyId, string accountId)
    {
        var reviews = await cosmo.GetReviewsAsync(strategyId);
        var review = reviews.FirstOrDefault(r => r.id == reviewId && r.ReviewerAccountId == accountId)
            ?? throw new InvalidOperationException("Review not found or you are not the author.");

        await cosmo.DeleteReviewAsync(reviewId, strategyId);
        await RefreshCommunityStatsAsync(strategyId);
    }

    /// <summary>
    /// Get the leaderboard of top-performing public strategies.
    /// Cached in memory with 5-minute TTL.
    /// </summary>
    public virtual async Task<List<LeaderboardEntry>> GetLeaderboardAsync(string sortBy = "sharpe", int limit = 25)
    {
        var cacheKey = $"{LeaderboardCachePrefix}{sortBy}:{limit}";
        if (cache.TryGetValue(cacheKey, out List<LeaderboardEntry>? cached) && cached != null)
            return cached;

        var strategies = await cosmo.GetPublicStrategiesAsync(0, 200);

        var entries = new List<LeaderboardEntry>();
        foreach (var strategy in strategies)
        {
            var backtests = await cosmo.GetBacktestsByStrategyIdAsync(strategy.id);
            var best = backtests.Where(b => b.Metrics != null).OrderByDescending(b => b.Metrics!.SharpeRatio).FirstOrDefault();
            if (best?.Metrics == null) continue;

            var stats = await cosmo.GetCommunityStatsAsync(strategy.id);

            entries.Add(new LeaderboardEntry
            {
                StrategyId = strategy.id,
                StrategyName = strategy.Name,
                AuthorName = strategy.UserId,
                AccountId = strategy.AccountId,
                Symbols = strategy.Symbols,
                TotalReturnPercent = best.Metrics.TotalReturnPercent,
                SharpeRatio = best.Metrics.SharpeRatio,
                MaxDrawdownPercent = best.Metrics.MaxDrawdownPercent,
                WinRate = best.Metrics.WinRate,
                TotalBacktests = backtests.Count,
                AverageRating = stats?.AverageRating ?? 0,
                ReviewCount = stats?.ReviewCount ?? 0,
                SubscriberCount = stats?.SubscriberCount ?? 0
            });
        }

        entries = sortBy?.ToLowerInvariant() switch
        {
            "return" => entries.OrderByDescending(e => e.TotalReturnPercent).ToList(),
            "winrate" => entries.OrderByDescending(e => e.WinRate).ToList(),
            _ => entries.OrderByDescending(e => e.SharpeRatio).ToList()
        };

        if (entries.Count > limit)
            entries = entries.Take(limit).ToList();

        cache.Set(cacheKey, entries, LeaderboardCacheDuration);
        return entries;
    }

    /// <summary>
    /// Recompute community stats from reviews, subscriptions, and forks.
    /// </summary>
    public virtual async Task RefreshCommunityStatsAsync(string strategyId)
    {
        var reviews = await cosmo.GetReviewsAsync(strategyId);
        var subscriptions = await cosmo.GetSubscriptionsByStrategyAsync(strategyId);
        var activeSubscribers = subscriptions.Count(s => s.IsActive);

        var existingStats = await cosmo.GetCommunityStatsAsync(strategyId);
        var forkCount = existingStats?.ForkCount ?? 0;

        var stats = new StrategyCommunityStats
        {
            id = strategyId,
            StrategyId = strategyId,
            AverageRating = reviews.Count > 0 ? (decimal)reviews.Average(r => r.Rating) : 0,
            ReviewCount = reviews.Count,
            SubscriberCount = activeSubscribers,
            ForkCount = forkCount
        };

        await cosmo.UpsertCommunityStatsAsync(stats);
    }

    private async Task IncrementForkCountAsync(string strategyId)
    {
        var stats = await cosmo.GetCommunityStatsAsync(strategyId) ?? new StrategyCommunityStats
        {
            id = strategyId,
            StrategyId = strategyId
        };

        stats.ForkCount++;
        await cosmo.UpsertCommunityStatsAsync(stats);
    }

    private static ConditionGroup CloneConditionGroup(ConditionGroup source) => new()
    {
        Logic = source.Logic,
        Conditions = source.Conditions.Select(c => new TriggerCondition
        {
            ConditionId = Guid.NewGuid().ToString("N")[..8],
            ConditionType = c.ConditionType,
            Operator = c.Operator,
            Value = c.Value,
            Period = c.Period
        }).ToList(),
        ChildGroups = source.ChildGroups.Select(CloneConditionGroup).ToList()
    };
}
