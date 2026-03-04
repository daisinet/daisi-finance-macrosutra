using global::MacroSutra.Core.Enums;
using global::MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace MacroSutra.Tests.Services;

public class StrategyPerformanceServiceTests
{
    private static (StrategyPerformanceService service, Mock<MacroSutraCosmo> cosmo) CreateSut()
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var service = new StrategyPerformanceService(cosmo.Object);
        return (service, cosmo);
    }

    [Fact]
    public async Task RecordTriggerAsync_CreatesTriggerRecord()
    {
        var (service, cosmo) = CreateSut();
        var trades = new List<Trade>
        {
            new() { id = "trd-1", Symbol = "AAPL", FilledPrice = 150m, Side = TradeSide.Buy, Quantity = 10 }
        };

        cosmo.Setup(c => c.CreateTriggerRecordAsync(It.IsAny<StrategyTriggerRecord>()))
            .ReturnsAsync((StrategyTriggerRecord r) => r);

        var result = await service.RecordTriggerAsync("acc1", "str1", "AAPL", trades);

        Assert.Equal("acc1", result.AccountId);
        Assert.Equal("str1", result.StrategyId);
        Assert.Equal("AAPL", result.Symbol);
        Assert.Contains("trd-1", result.TradeIds);
        Assert.Equal(TriggerOutcome.Open, result.Outcome);
        cosmo.Verify(c => c.CreateTriggerRecordAsync(It.IsAny<StrategyTriggerRecord>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTriggerOutcomeAsync_WinningTrade_SetsWin()
    {
        var (service, cosmo) = CreateSut();
        var record = new StrategyTriggerRecord
        {
            id = "trig-1", AccountId = "acc1", EntryPrice = 100m, Outcome = TriggerOutcome.Open,
            TradeIds = new List<string> { "trd-1" }
        };

        cosmo.Setup(c => c.GetOpenTriggerRecordsByTradeIdAsync("trd-1"))
            .ReturnsAsync(new List<StrategyTriggerRecord> { record });
        cosmo.Setup(c => c.UpdateTriggerRecordAsync(It.IsAny<StrategyTriggerRecord>()))
            .ReturnsAsync((StrategyTriggerRecord r) => r);

        await service.UpdateTriggerOutcomeAsync("trd-1", 120m, 10m);

        Assert.Equal(TriggerOutcome.Win, record.Outcome);
        Assert.Equal(120m, record.ExitPrice);
        Assert.Equal(200m, record.PnL); // (120-100)*10
        Assert.Equal(20m, record.ReturnPercent); // ((120-100)/100)*100
    }

    [Fact]
    public async Task UpdateTriggerOutcomeAsync_LosingTrade_SetsLoss()
    {
        var (service, cosmo) = CreateSut();
        var record = new StrategyTriggerRecord
        {
            id = "trig-1", AccountId = "acc1", EntryPrice = 100m, Outcome = TriggerOutcome.Open,
            TradeIds = new List<string> { "trd-1" }
        };

        cosmo.Setup(c => c.GetOpenTriggerRecordsByTradeIdAsync("trd-1"))
            .ReturnsAsync(new List<StrategyTriggerRecord> { record });
        cosmo.Setup(c => c.UpdateTriggerRecordAsync(It.IsAny<StrategyTriggerRecord>()))
            .ReturnsAsync((StrategyTriggerRecord r) => r);

        await service.UpdateTriggerOutcomeAsync("trd-1", 80m, 10m);

        Assert.Equal(TriggerOutcome.Loss, record.Outcome);
        Assert.Equal(-200m, record.PnL); // (80-100)*10
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_ComputesWinRate()
    {
        var (service, cosmo) = CreateSut();
        var records = new List<StrategyTriggerRecord>
        {
            new() { Outcome = TriggerOutcome.Win, PnL = 100, ReturnPercent = 10, TriggeredUtc = new DateTime(2026, 1, 15) },
            new() { Outcome = TriggerOutcome.Win, PnL = 50, ReturnPercent = 5, TriggeredUtc = new DateTime(2026, 1, 20) },
            new() { Outcome = TriggerOutcome.Loss, PnL = -30, ReturnPercent = -3, TriggeredUtc = new DateTime(2026, 2, 1) },
            new() { Outcome = TriggerOutcome.Open, TriggeredUtc = new DateTime(2026, 2, 5) }
        };

        cosmo.Setup(c => c.GetTriggerRecordsAsync("acc1", "str1")).ReturnsAsync(records);

        var summary = await service.GetPerformanceSummaryAsync("acc1", "str1");

        Assert.Equal(4, summary.TotalTriggers);
        Assert.Equal(2, summary.Wins);
        Assert.Equal(1, summary.Losses);
        Assert.Equal(1, summary.OpenTrades);
        Assert.Equal(120m, summary.TotalPnL);
        // Win rate: 2 wins / (2+1) closed = 66.67%
        Assert.True(summary.WinRate > 66 && summary.WinRate < 67);
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_EmptyRecords_ReturnsZeros()
    {
        var (service, cosmo) = CreateSut();
        cosmo.Setup(c => c.GetTriggerRecordsAsync("acc1", "str1"))
            .ReturnsAsync(new List<StrategyTriggerRecord>());

        var summary = await service.GetPerformanceSummaryAsync("acc1", "str1");

        Assert.Equal(0, summary.TotalTriggers);
        Assert.Equal(0, summary.WinRate);
        Assert.Equal(0, summary.TotalPnL);
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_MonthlyReturnsGrouped()
    {
        var (service, cosmo) = CreateSut();
        var records = new List<StrategyTriggerRecord>
        {
            new() { Outcome = TriggerOutcome.Win, PnL = 100, ReturnPercent = 10, TriggeredUtc = new DateTime(2026, 1, 15) },
            new() { Outcome = TriggerOutcome.Win, PnL = 50, ReturnPercent = 5, TriggeredUtc = new DateTime(2026, 1, 20) },
            new() { Outcome = TriggerOutcome.Loss, PnL = -30, ReturnPercent = -3, TriggeredUtc = new DateTime(2026, 2, 1) }
        };

        cosmo.Setup(c => c.GetTriggerRecordsAsync("acc1", "str1")).ReturnsAsync(records);

        var summary = await service.GetPerformanceSummaryAsync("acc1", "str1");

        Assert.Equal(2, summary.MonthlyReturns.Count);
        Assert.Equal(15m, summary.MonthlyReturns[0].ReturnPercent); // Jan: 10+5
        Assert.Equal(2, summary.MonthlyReturns[0].Triggers);
        Assert.Equal(-3m, summary.MonthlyReturns[1].ReturnPercent); // Feb: -3
    }

    [Fact]
    public async Task GetTriggerHistoryAsync_DelegatesToCosmo()
    {
        var (service, cosmo) = CreateSut();
        var expected = new List<StrategyTriggerRecord> { new() { id = "trig-1" } };
        cosmo.Setup(c => c.GetTriggerRecordsAsync("acc1", "str1")).ReturnsAsync(expected);

        var result = await service.GetTriggerHistoryAsync("acc1", "str1");

        Assert.Single(result);
        Assert.Equal("trig-1", result[0].id);
    }

    [Fact]
    public async Task UpdateTriggerOutcomeAsync_NoRecordsFound_NoOp()
    {
        var (service, cosmo) = CreateSut();
        cosmo.Setup(c => c.GetOpenTriggerRecordsByTradeIdAsync("trd-99"))
            .ReturnsAsync(new List<StrategyTriggerRecord>());

        await service.UpdateTriggerOutcomeAsync("trd-99", 100m, 10m);

        cosmo.Verify(c => c.UpdateTriggerRecordAsync(It.IsAny<StrategyTriggerRecord>()), Times.Never);
    }
}
