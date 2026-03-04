using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace MacroSutra.Tests.Services;

public class SubscriptionServiceTests
{
    private static (
        SubscriptionService service,
        Mock<MacroSutraCosmo> cosmo
    ) CreateSut()
    {
        var config = Mock.Of<IConfiguration>();
        var cosmo = new Mock<MacroSutraCosmo>(config, "Cosmo:ConnectionString");
        var strategyService = new StrategyService(cosmo.Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var communityService = new CommunityService(cosmo.Object, strategyService, cache);
        var logger = Mock.Of<ILogger<SubscriptionService>>();

        var service = new SubscriptionService(cosmo.Object, communityService, logger);
        return (service, cosmo);
    }

    private static void SetupCommunityStatsRefresh(Mock<MacroSutraCosmo> cosmo)
    {
        cosmo.Setup(c => c.GetReviewsAsync(It.IsAny<string>())).ReturnsAsync(new List<StrategyReview>());
        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync(It.IsAny<string>())).ReturnsAsync(new List<Subscription>());
        cosmo.Setup(c => c.GetCommunityStatsAsync(It.IsAny<string>())).ReturnsAsync((StrategyCommunityStats?)null);
        cosmo.Setup(c => c.UpsertCommunityStatsAsync(It.IsAny<StrategyCommunityStats>()))
             .ReturnsAsync((StrategyCommunityStats s) => s);
    }

    [Fact]
    public async Task SubscribeAsync_CreatesSubscription()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetActiveSubscriptionAsync("acc-sub", "str-1")).ReturnsAsync((Subscription?)null);
        cosmo.Setup(c => c.CreateSubscriptionAsync(It.IsAny<Subscription>()))
             .ReturnsAsync((Subscription s) => { s.id = "sub-1"; return s; });
        SetupCommunityStatsRefresh(cosmo);

        var subscription = new Subscription
        {
            AccountId = "acc-sub", StrategyId = "str-1", CreditPrice = 0
        };

        var result = await service.SubscribeAsync(subscription);

        Assert.Equal("sub-1", result.id);
        cosmo.Verify(c => c.CreateSubscriptionAsync(It.IsAny<Subscription>()), Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_DuplicateActiveSubscription_Throws()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetActiveSubscriptionAsync("acc-sub", "str-1"))
             .ReturnsAsync(new Subscription { id = "existing-sub" });

        var subscription = new Subscription
        {
            AccountId = "acc-sub", StrategyId = "str-1"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubscribeAsync(subscription));
    }

    [Fact]
    public async Task SubscribeAsync_RefreshesCommunityStats()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetActiveSubscriptionAsync("acc-sub", "str-1")).ReturnsAsync((Subscription?)null);
        cosmo.Setup(c => c.CreateSubscriptionAsync(It.IsAny<Subscription>()))
             .ReturnsAsync((Subscription s) => { s.id = "sub-1"; return s; });
        SetupCommunityStatsRefresh(cosmo);

        var subscription = new Subscription
        {
            AccountId = "acc-sub", StrategyId = "str-1", CreditPrice = 0
        };

        await service.SubscribeAsync(subscription);

        // Verify community stats were refreshed
        cosmo.Verify(c => c.UpsertCommunityStatsAsync(It.IsAny<StrategyCommunityStats>()), Times.Once);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_SetsInactive_RefreshesStats()
    {
        var (service, cosmo) = CreateSut();

        var subscription = new Subscription
        {
            id = "sub-1", AccountId = "acc-sub", StrategyId = "str-1", IsActive = true
        };

        cosmo.Setup(c => c.GetSubscriptionAsync("sub-1", "acc-sub")).ReturnsAsync(subscription);
        cosmo.Setup(c => c.UpdateSubscriptionAsync(It.IsAny<Subscription>()))
             .ReturnsAsync((Subscription s) => s);
        SetupCommunityStatsRefresh(cosmo);

        var result = await service.CancelSubscriptionAsync("sub-1", "acc-sub");

        Assert.False(result.IsActive);
        cosmo.Verify(c => c.UpdateSubscriptionAsync(It.Is<Subscription>(s => !s.IsActive)), Times.Once);
        cosmo.Verify(c => c.UpsertCommunityStatsAsync(It.IsAny<StrategyCommunityStats>()), Times.Once);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_NotFound_Throws()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetSubscriptionAsync("sub-missing", "acc-sub")).ReturnsAsync((Subscription?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CancelSubscriptionAsync("sub-missing", "acc-sub"));
    }
}
