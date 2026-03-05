using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;

namespace MacroSutra.Tests.Services;

public class CommunityServiceTests
{
    private static (CommunityService service, Mock<MacroSutraCosmo> cosmo) CreateSut()
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var strategyService = new StrategyService(cosmo.Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CommunityService(cosmo.Object, strategyService, cache);
        return (service, cosmo);
    }

    // ── Fork Tests ──

    [Fact]
    public async Task ForkStrategyAsync_CopiesAllFields_SetsAttribution()
    {
        var (service, cosmo) = CreateSut();
        var source = new TradingStrategy
        {
            id = "str-source", AccountId = "acc-author", UserId = "u1",
            Name = "Bull Run", Description = "Buy the dip",
            Symbols = new List<string> { "AAPL", "MSFT" },
            Visibility = StrategyVisibility.Public,
            IsActive = true,
            TriggerGroups = new()
            {
                new TriggerGroup
                {
                    Name = "Buy Signal",
                    Conditions = new ConditionGroup
                    {
                        Conditions = new() { new() { ConditionType = ConditionType.RSI, Operator = ConditionOperator.LessThan, Value = 30 } }
                    },
                    Actions = new() { new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Buy, Quantity = 10 } }
                }
            }
        };

        cosmo.Setup(c => c.GetPublicStrategyAsync("str-source")).ReturnsAsync(source);
        cosmo.Setup(c => c.CreateStrategyAsync(It.IsAny<TradingStrategy>()))
             .ReturnsAsync((TradingStrategy s) => { s.id = "str-fork"; return s; });
        cosmo.Setup(c => c.GetCommunityStatsAsync("str-source")).ReturnsAsync((StrategyCommunityStats?)null);
        cosmo.Setup(c => c.UpsertCommunityStatsAsync(It.IsAny<StrategyCommunityStats>()))
             .ReturnsAsync((StrategyCommunityStats s) => s);

        var result = await service.ForkStrategyAsync("str-source", "acc-forker", "u-forker");

        Assert.Equal("acc-forker", result.AccountId);
        Assert.Equal("u-forker", result.UserId);
        Assert.Contains("Fork", result.Name);
        Assert.Equal("Buy the dip", result.Description);
        Assert.Equal(2, result.Symbols.Count);
        Assert.Single(result.TriggerGroups);
        Assert.Single(result.TriggerGroups[0].Conditions.Conditions);
        Assert.Single(result.TriggerGroups[0].Actions);
        Assert.False(result.IsActive);
        Assert.Equal(StrategyVisibility.Private, result.Visibility);
        Assert.Equal("str-source", result.ForkedFromStrategyId);
        Assert.Equal("acc-author", result.ForkedFromAccountId);
    }

    [Fact]
    public async Task ForkStrategyAsync_NonPublic_Throws()
    {
        var (service, cosmo) = CreateSut();
        cosmo.Setup(c => c.GetPublicStrategyAsync("str-1")).ReturnsAsync((TradingStrategy?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ForkStrategyAsync("str-1", "acc1", "u1"));
    }

    // ── Review Tests ──

    [Fact]
    public async Task CreateReviewAsync_ValidRating_CreatesReview()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetReviewByUserAsync("str-1", "acc-rev")).ReturnsAsync((StrategyReview?)null);
        cosmo.Setup(c => c.CreateReviewAsync(It.IsAny<StrategyReview>()))
             .ReturnsAsync((StrategyReview r) => { r.id = "rev-1"; return r; });
        cosmo.Setup(c => c.GetReviewsAsync("str-1")).ReturnsAsync(new List<StrategyReview>
        {
            new() { Rating = 4 }
        });
        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1")).ReturnsAsync(new List<Subscription>());
        cosmo.Setup(c => c.GetCommunityStatsAsync("str-1")).ReturnsAsync((StrategyCommunityStats?)null);
        cosmo.Setup(c => c.UpsertCommunityStatsAsync(It.IsAny<StrategyCommunityStats>()))
             .ReturnsAsync((StrategyCommunityStats s) => s);

        var review = await service.CreateReviewAsync("str-1", "acc-rev", "u-rev", "TestUser", 4, "Great strategy!");

        Assert.Equal("rev-1", review.id);
        Assert.Equal(4, review.Rating);
        Assert.Equal("Great strategy!", review.ReviewText);
    }

    [Fact]
    public async Task CreateReviewAsync_InvalidRating_Throws()
    {
        var (service, _) = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateReviewAsync("str-1", "acc-1", "u1", "User", 0, null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateReviewAsync("str-1", "acc-1", "u1", "User", 6, null));
    }

    [Fact]
    public async Task CreateReviewAsync_DuplicateReview_Throws()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetReviewByUserAsync("str-1", "acc-1"))
             .ReturnsAsync(new StrategyReview { id = "rev-existing" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateReviewAsync("str-1", "acc-1", "u1", "User", 5, null));
    }

    [Fact]
    public async Task DeleteReviewAsync_RecomputesStats()
    {
        var (service, cosmo) = CreateSut();

        var reviews = new List<StrategyReview>
        {
            new() { id = "rev-1", ReviewerAccountId = "acc-1", Rating = 5 },
            new() { id = "rev-2", ReviewerAccountId = "acc-2", Rating = 3 }
        };

        cosmo.Setup(c => c.GetReviewsAsync("str-1")).ReturnsAsync(reviews);
        cosmo.Setup(c => c.DeleteReviewAsync("rev-1", "str-1")).Returns(Task.CompletedTask);
        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1")).ReturnsAsync(new List<Subscription>());
        cosmo.Setup(c => c.GetCommunityStatsAsync("str-1")).ReturnsAsync((StrategyCommunityStats?)null);
        cosmo.Setup(c => c.UpsertCommunityStatsAsync(It.IsAny<StrategyCommunityStats>()))
             .ReturnsAsync((StrategyCommunityStats s) => s);

        await service.DeleteReviewAsync("rev-1", "str-1", "acc-1");

        cosmo.Verify(c => c.DeleteReviewAsync("rev-1", "str-1"), Times.Once);
        cosmo.Verify(c => c.UpsertCommunityStatsAsync(It.IsAny<StrategyCommunityStats>()), Times.Once);
    }

    // ── Community Stats Tests ──

    [Fact]
    public async Task RefreshCommunityStatsAsync_ComputesAverageCorrectly()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetReviewsAsync("str-1")).ReturnsAsync(new List<StrategyReview>
        {
            new() { Rating = 5 },
            new() { Rating = 3 },
            new() { Rating = 4 }
        });
        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1")).ReturnsAsync(new List<Subscription>
        {
            new() { IsActive = true },
            new() { IsActive = false },
            new() { IsActive = true }
        });
        cosmo.Setup(c => c.GetCommunityStatsAsync("str-1")).ReturnsAsync(new StrategyCommunityStats { ForkCount = 2 });

        StrategyCommunityStats? captured = null;
        cosmo.Setup(c => c.UpsertCommunityStatsAsync(It.IsAny<StrategyCommunityStats>()))
             .Callback<StrategyCommunityStats>(s => captured = s)
             .ReturnsAsync((StrategyCommunityStats s) => s);

        await service.RefreshCommunityStatsAsync("str-1");

        Assert.NotNull(captured);
        Assert.Equal(4.0m, captured!.AverageRating);
        Assert.Equal(3, captured.ReviewCount);
        Assert.Equal(2, captured.SubscriberCount);
        Assert.Equal(2, captured.ForkCount);
    }

    // ── Leaderboard Tests ──

    [Fact]
    public async Task GetLeaderboardAsync_SortsBySharpe_LimitsResults()
    {
        var (service, cosmo) = CreateSut();

        var strategies = new List<TradingStrategy>
        {
            new() { id = "s1", Name = "Alpha", Symbols = new() { "AAPL" }, Visibility = StrategyVisibility.Public },
            new() { id = "s2", Name = "Beta", Symbols = new() { "MSFT" }, Visibility = StrategyVisibility.Public },
            new() { id = "s3", Name = "Gamma", Symbols = new() { "TSLA" }, Visibility = StrategyVisibility.Public }
        };

        cosmo.Setup(c => c.GetPublicStrategiesAsync(0, 200, null)).ReturnsAsync(strategies);

        cosmo.Setup(c => c.GetBacktestsByStrategyIdAsync("s1")).ReturnsAsync(new List<BacktestResult>
        {
            new() { Metrics = new BacktestMetrics { SharpeRatio = 1.5m, TotalReturnPercent = 20, WinRate = 60, MaxDrawdownPercent = -10 } }
        });
        cosmo.Setup(c => c.GetBacktestsByStrategyIdAsync("s2")).ReturnsAsync(new List<BacktestResult>
        {
            new() { Metrics = new BacktestMetrics { SharpeRatio = 2.0m, TotalReturnPercent = 30, WinRate = 70, MaxDrawdownPercent = -5 } }
        });
        cosmo.Setup(c => c.GetBacktestsByStrategyIdAsync("s3")).ReturnsAsync(new List<BacktestResult>());

        cosmo.Setup(c => c.GetCommunityStatsAsync(It.IsAny<string>())).ReturnsAsync((StrategyCommunityStats?)null);

        var result = await service.GetLeaderboardAsync("sharpe", 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("Beta", result[0].StrategyName);
        Assert.Equal("Alpha", result[1].StrategyName);
    }

    [Fact]
    public async Task GetLeaderboardAsync_SortsByReturn()
    {
        var (service, cosmo) = CreateSut();

        var strategies = new List<TradingStrategy>
        {
            new() { id = "s1", Name = "Alpha", Symbols = new() { "AAPL" }, Visibility = StrategyVisibility.Public },
            new() { id = "s2", Name = "Beta", Symbols = new() { "MSFT" }, Visibility = StrategyVisibility.Public }
        };

        cosmo.Setup(c => c.GetPublicStrategiesAsync(0, 200, null)).ReturnsAsync(strategies);

        cosmo.Setup(c => c.GetBacktestsByStrategyIdAsync("s1")).ReturnsAsync(new List<BacktestResult>
        {
            new() { Metrics = new BacktestMetrics { SharpeRatio = 2.0m, TotalReturnPercent = 50 } }
        });
        cosmo.Setup(c => c.GetBacktestsByStrategyIdAsync("s2")).ReturnsAsync(new List<BacktestResult>
        {
            new() { Metrics = new BacktestMetrics { SharpeRatio = 1.0m, TotalReturnPercent = 80 } }
        });

        cosmo.Setup(c => c.GetCommunityStatsAsync(It.IsAny<string>())).ReturnsAsync((StrategyCommunityStats?)null);

        var result = await service.GetLeaderboardAsync("return", 25);

        Assert.Equal("Beta", result[0].StrategyName);
        Assert.Equal("Alpha", result[1].StrategyName);
    }

    // ── Public Strategy Query Tests ──

    [Fact]
    public async Task GetPublicStrategiesAsync_DelegatesToCosmo()
    {
        var (service, cosmo) = CreateSut();
        var strategies = new List<TradingStrategy>
        {
            new() { id = "s1", Name = "Public One", Visibility = StrategyVisibility.Public }
        };
        cosmo.Setup(c => c.GetPublicStrategiesAsync(0, 20, null)).ReturnsAsync(strategies);

        var result = await service.GetPublicStrategiesAsync();

        Assert.Single(result);
        Assert.Equal("Public One", result[0].Name);
    }
}
