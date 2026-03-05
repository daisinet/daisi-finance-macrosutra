using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace MacroSutra.Tests.Services;

/// <summary>
/// WU6: Portfolio rebalancing tests.
/// </summary>
public class RebalanceServiceTests
{
    private static (RebalanceService service, Mock<MacroSutraCosmo> cosmo, Mock<BrokerageProviderFactory> factory) CreateSut()
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var factory = new Mock<BrokerageProviderFactory>(Mock.Of<IServiceProvider>());
        var portfolioService = new PortfolioService(cosmo.Object, factory.Object);
        var tradeService = new TradeService(cosmo.Object);
        var logger = Mock.Of<ILogger<RebalanceService>>();

        cosmo.Setup(c => c.CreateTradeAsync(It.IsAny<Trade>()))
             .ReturnsAsync((Trade t) => { t.id = "trd-reb-1"; return t; });
        cosmo.Setup(c => c.UpdateTradeAsync(It.IsAny<Trade>()))
             .ReturnsAsync((Trade t) => t);
        cosmo.Setup(c => c.GetTradeAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync((string id, string aid) => new Trade { id = id, AccountId = aid, Status = TradeStatus.Pending });

        var service = new RebalanceService(cosmo.Object, portfolioService, tradeService, factory.Object, logger);
        return (service, cosmo, factory);
    }

    private static RebalanceTarget MakeTarget(decimal driftThreshold = 5m) => new()
    {
        id = "rt-1",
        AccountId = "acc1",
        Name = "60/40 Portfolio",
        BrokerageAccountId = "bra-1",
        DriftThresholdPercent = driftThreshold,
        Allocations =
        [
            new AllocationTarget { Symbol = "VTI", TargetPercent = 60 },
            new AllocationTarget { Symbol = "BND", TargetPercent = 40 }
        ]
    };

    [Fact]
    public async Task AnalyzeAsync_NoDrift_ReturnsNeedsRebalancingFalse()
    {
        var (service, cosmo, _) = CreateSut();
        var target = MakeTarget();

        cosmo.Setup(c => c.GetRebalanceTargetAsync("rt-1", "acc1")).ReturnsAsync(target);
        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(
        [
            new Position { Symbol = "VTI", Quantity = 60, AverageCost = 100, CurrentPrice = 100 },
            new Position { Symbol = "BND", Quantity = 40, AverageCost = 100, CurrentPrice = 100 }
        ]);

        var analysis = await service.AnalyzeAsync("rt-1", "acc1");

        Assert.False(analysis.NeedsRebalancing);
        Assert.Equal(10000m, analysis.TotalPortfolioValue);
    }

    [Fact]
    public async Task AnalyzeAsync_DriftAboveThreshold_ReturnsNeedsRebalancingTrue()
    {
        var (service, cosmo, _) = CreateSut();
        var target = MakeTarget();

        cosmo.Setup(c => c.GetRebalanceTargetAsync("rt-1", "acc1")).ReturnsAsync(target);
        // VTI is 80% of portfolio, BND is 20% → drift of 20%
        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(
        [
            new Position { Symbol = "VTI", Quantity = 80, AverageCost = 100, CurrentPrice = 100 },
            new Position { Symbol = "BND", Quantity = 20, AverageCost = 100, CurrentPrice = 100 }
        ]);

        var analysis = await service.AnalyzeAsync("rt-1", "acc1");

        Assert.True(analysis.NeedsRebalancing);
    }

    [Fact]
    public async Task AnalyzeAsync_CorrectDriftPercent()
    {
        var (service, cosmo, _) = CreateSut();
        var target = MakeTarget();

        cosmo.Setup(c => c.GetRebalanceTargetAsync("rt-1", "acc1")).ReturnsAsync(target);
        // VTI: 80%, target 60% → drift +20%
        // BND: 20%, target 40% → drift -20%
        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(
        [
            new Position { Symbol = "VTI", Quantity = 80, AverageCost = 100, CurrentPrice = 100 },
            new Position { Symbol = "BND", Quantity = 20, AverageCost = 100, CurrentPrice = 100 }
        ]);

        var analysis = await service.AnalyzeAsync("rt-1", "acc1");

        var vtiDrift = analysis.Drifts.First(d => d.Symbol == "VTI");
        var bndDrift = analysis.Drifts.First(d => d.Symbol == "BND");

        Assert.Equal(80m, vtiDrift.ActualPercent);
        Assert.Equal(20m, vtiDrift.DriftPercent);
        Assert.Equal(20m, bndDrift.ActualPercent);
        Assert.Equal(-20m, bndDrift.DriftPercent);
    }

    [Fact]
    public async Task AnalyzeAsync_SuggestsCorrectTradeValues()
    {
        var (service, cosmo, _) = CreateSut();
        var target = MakeTarget();

        cosmo.Setup(c => c.GetRebalanceTargetAsync("rt-1", "acc1")).ReturnsAsync(target);
        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(
        [
            new Position { Symbol = "VTI", Quantity = 80, AverageCost = 100, CurrentPrice = 100 },
            new Position { Symbol = "BND", Quantity = 20, AverageCost = 100, CurrentPrice = 100 }
        ]);

        var analysis = await service.AnalyzeAsync("rt-1", "acc1");

        // VTI overweight: should sell, suggested trade value is negative
        var vtiDrift = analysis.Drifts.First(d => d.Symbol == "VTI");
        Assert.True(vtiDrift.SuggestedTradeValue < 0, "Overweight VTI should have negative suggested trade value");

        // BND underweight: should buy, suggested trade value is positive
        var bndDrift = analysis.Drifts.First(d => d.Symbol == "BND");
        Assert.True(bndDrift.SuggestedTradeValue > 0, "Underweight BND should have positive suggested trade value");
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyPositions_ReturnsZeroPortfolioValue()
    {
        var (service, cosmo, _) = CreateSut();
        var target = MakeTarget();

        cosmo.Setup(c => c.GetRebalanceTargetAsync("rt-1", "acc1")).ReturnsAsync(target);
        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync([]);

        var analysis = await service.AnalyzeAsync("rt-1", "acc1");

        Assert.False(analysis.NeedsRebalancing);
        Assert.Equal(0m, analysis.TotalPortfolioValue);
    }

    [Fact]
    public async Task AnalyzeAsync_TargetNotFound_Throws()
    {
        var (service, cosmo, _) = CreateSut();
        cosmo.Setup(c => c.GetRebalanceTargetAsync("rt-missing", "acc1")).ReturnsAsync((RebalanceTarget?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync("rt-missing", "acc1"));
    }

    [Fact]
    public async Task AnalyzeAsync_SmallDrift_DoesNotTriggerRebalance()
    {
        var (service, cosmo, _) = CreateSut();
        var target = MakeTarget(driftThreshold: 5m);

        cosmo.Setup(c => c.GetRebalanceTargetAsync("rt-1", "acc1")).ReturnsAsync(target);
        // VTI at 62% (drift +2%), BND at 38% (drift -2%) — within 5% threshold
        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(
        [
            new Position { Symbol = "VTI", Quantity = 62, AverageCost = 100, CurrentPrice = 100 },
            new Position { Symbol = "BND", Quantity = 38, AverageCost = 100, CurrentPrice = 100 }
        ]);

        var analysis = await service.AnalyzeAsync("rt-1", "acc1");

        Assert.False(analysis.NeedsRebalancing);
    }
}
