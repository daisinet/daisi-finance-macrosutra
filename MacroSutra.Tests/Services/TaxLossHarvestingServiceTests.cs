using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace MacroSutra.Tests.Services;

/// <summary>
/// WU7: Tax-loss harvesting analysis tests.
/// </summary>
public class TaxLossHarvestingServiceTests
{
    private static (TaxLossHarvestingService service, Mock<MacroSutraCosmo> cosmo) CreateSut()
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var factory = new Mock<BrokerageProviderFactory>(Mock.Of<IServiceProvider>());
        var portfolioService = new PortfolioService(cosmo.Object, factory.Object);
        var tradeService = new TradeService(cosmo.Object);

        var service = new TaxLossHarvestingService(portfolioService, tradeService);
        return (service, cosmo);
    }

    [Fact]
    public async Task AnalyzeAsync_LossPosition_IncludedInCandidates()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync(
        [
            new Position { Symbol = "AAPL", Quantity = 100, AverageCost = 200, CurrentPrice = 150 }
        ]);
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync([]);

        var report = await service.AnalyzeAsync("acc1");

        Assert.Single(report.Candidates);
        Assert.Equal("AAPL", report.Candidates[0].Symbol);
        Assert.Equal(5000m, report.Candidates[0].UnrealizedLoss); // (200-150)*100 = 5000
    }

    [Fact]
    public async Task AnalyzeAsync_GainPosition_ExcludedFromCandidates()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync(
        [
            new Position { Symbol = "AAPL", Quantity = 100, AverageCost = 100, CurrentPrice = 150 } // gain
        ]);
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync([]);

        var report = await service.AnalyzeAsync("acc1");

        Assert.Empty(report.Candidates);
    }

    [Fact]
    public async Task AnalyzeAsync_RecentBuy_MarksWashSaleRisk()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync(
        [
            new Position { Symbol = "AAPL", Quantity = 100, AverageCost = 200, CurrentPrice = 150 }
        ]);
        // Bought AAPL 5 days ago — within 30-day wash sale window
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync(
        [
            new Trade { Symbol = "AAPL", Side = TradeSide.Buy, CreatedUtc = DateTime.UtcNow.AddDays(-5) }
        ]);

        var report = await service.AnalyzeAsync("acc1");

        Assert.Single(report.Candidates);
        Assert.True(report.Candidates[0].WashSaleRisk);
    }

    [Fact]
    public async Task AnalyzeAsync_OldBuy_NoWashSaleRisk()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync(
        [
            new Position { Symbol = "AAPL", Quantity = 100, AverageCost = 200, CurrentPrice = 150 }
        ]);
        // Bought AAPL 60 days ago — outside 30-day window
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync(
        [
            new Trade { Symbol = "AAPL", Side = TradeSide.Buy, CreatedUtc = DateTime.UtcNow.AddDays(-60) }
        ]);

        var report = await service.AnalyzeAsync("acc1");

        Assert.Single(report.Candidates);
        Assert.False(report.Candidates[0].WashSaleRisk);
    }

    [Fact]
    public async Task AnalyzeAsync_WashSale_TaxBenefitIsZero()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync(
        [
            new Position { Symbol = "AAPL", Quantity = 100, AverageCost = 200, CurrentPrice = 150 }
        ]);
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync(
        [
            new Trade { Symbol = "AAPL", Side = TradeSide.Buy, CreatedUtc = DateTime.UtcNow.AddDays(-10) }
        ]);

        var report = await service.AnalyzeAsync("acc1");

        Assert.Equal(0m, report.Candidates[0].EstimatedTaxBenefit);
    }

    [Fact]
    public async Task AnalyzeAsync_NoWashSale_TaxBenefitIs20PercentOfLoss()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync(
        [
            new Position { Symbol = "AAPL", Quantity = 100, AverageCost = 200, CurrentPrice = 150 }
        ]);
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync([]);

        var report = await service.AnalyzeAsync("acc1");

        // Loss = 5000, benefit = 5000 * 0.20 = 1000
        Assert.Equal(1000m, report.Candidates[0].EstimatedTaxBenefit);
        Assert.Equal(1000m, report.TotalEstimatedTaxBenefit);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyPositions_ReturnsEmptyReport()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync([]);
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync([]);

        var report = await service.AnalyzeAsync("acc1");

        Assert.Empty(report.Candidates);
        Assert.Equal(0m, report.TotalEstimatedLoss);
        Assert.Equal(0m, report.TotalEstimatedTaxBenefit);
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleLossPositions_SumsTotals()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync(
        [
            new Position { Symbol = "AAPL", Quantity = 100, AverageCost = 200, CurrentPrice = 150 },
            new Position { Symbol = "MSFT", Quantity = 50, AverageCost = 400, CurrentPrice = 350 }
        ]);
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync([]);

        var report = await service.AnalyzeAsync("acc1");

        Assert.Equal(2, report.Candidates.Count);
        // AAPL loss: 5000, MSFT loss: 2500
        Assert.Equal(7500m, report.TotalEstimatedLoss);
        Assert.Equal(1500m, report.TotalEstimatedTaxBenefit); // 7500 * 0.20
    }

    [Fact]
    public async Task AnalyzeAsync_OptionPosition_ExcludedFromCandidates()
    {
        var (service, cosmo) = CreateSut();

        cosmo.Setup(c => c.GetPositionsAsync("acc1", null)).ReturnsAsync(
        [
            new Position
            {
                Symbol = "AAPL 240620C200", Quantity = 5, AverageCost = 10, CurrentPrice = 5,
                OptionDetails = new MacroSutra.Core.Models.Options.OptionDetails
                {
                    ContractSymbol = "AAPL 240620C200",
                    UnderlyingSymbol = "AAPL",
                    OptionType = MacroSutra.Core.Enums.OptionType.Call
                }
            }
        ]);
        cosmo.Setup(c => c.GetTradesAsync("acc1", null, null, null)).ReturnsAsync([]);

        var report = await service.AnalyzeAsync("acc1");

        Assert.Empty(report.Candidates);
    }
}
