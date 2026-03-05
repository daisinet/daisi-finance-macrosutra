using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace MacroSutra.Tests.Services;

public class WalkForwardServiceTests
{
    private static (
        WalkForwardService service,
        Mock<StrategyService> strategyService,
        Mock<MarketDataService> marketDataService
    ) CreateSut()
    {
        var config = Mock.Of<IConfiguration>();
        var cosmo = new Mock<MacroSutraCosmo>(config, "Cosmo:ConnectionString");

        var strategyService = new Mock<StrategyService>(cosmo.Object);
        var marketDataService = new Mock<MarketDataService>(config, Mock.Of<ILogger<MarketDataService>>());
        var engine = new BacktestEngine(new ConditionEvaluator());

        var service = new WalkForwardService(strategyService.Object, marketDataService.Object, engine);
        return (service, strategyService, marketDataService);
    }

    /// <summary>
    /// Strategy that never triggers (price threshold impossibly high).
    /// Backtest returns 0% for every window.
    /// </summary>
    private static TradingStrategy MakeNeverTriggersStrategy() => new()
    {
        id = "str-1",
        AccountId = "acc-1",
        Name = "Never Triggers",
        Symbols = new List<string> { "SPY" },
        TriggerGroups = new()
        {
            new TriggerGroup
            {
                Name = "Test",
                Conditions = new ConditionGroup
                {
                    Logic = LogicGroupType.And,
                    Conditions = new() { new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 999_999 } }
                },
                Actions = new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 } }
            }
        }
    };

    /// <summary>
    /// Strategy that always triggers (price > 0) and buys only.
    /// Position is force-closed at the last bar of each window.
    /// With rising prices this produces positive returns.
    /// </summary>
    private static TradingStrategy MakeBuyOnlyStrategy() => new()
    {
        id = "str-1",
        AccountId = "acc-1",
        Name = "Buy Only",
        Symbols = new List<string> { "SPY" },
        TriggerGroups = new()
        {
            new TriggerGroup
            {
                Name = "Test",
                Conditions = new ConditionGroup
                {
                    Logic = LogicGroupType.And,
                    Conditions = new() { new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 } }
                },
                Actions = new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 } }
            }
        }
    };

    /// <summary>
    /// Generates N daily bars starting from a given date with rising prices.
    /// </summary>
    private static List<OhlcvBar> MakeRisingBars(int count, DateOnly start, decimal startPrice = 100m, decimal dailyIncrease = 1m)
    {
        var bars = new List<OhlcvBar>();
        for (int i = 0; i < count; i++)
        {
            var close = startPrice + i * dailyIncrease;
            bars.Add(new OhlcvBar(start.AddDays(i), close - 1, close + 2, close - 2, close, 1_000_000));
        }
        return bars;
    }

    /// <summary>
    /// Generates N daily bars starting from a given date with falling prices.
    /// </summary>
    private static List<OhlcvBar> MakeFallingBars(int count, DateOnly start, decimal startPrice = 200m, decimal dailyDecrease = 1m)
    {
        var bars = new List<OhlcvBar>();
        for (int i = 0; i < count; i++)
        {
            var close = startPrice - i * dailyDecrease;
            bars.Add(new OhlcvBar(start.AddDays(i), close - 1, close + 2, close - 2, close, 1_000_000));
        }
        return bars;
    }

    [Fact]
    public async Task EmptyBars_ReturnsEmptySummary()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        var strategy = MakeNeverTriggersStrategy();

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<OhlcvBar>());

        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 100_000);

        Assert.NotNull(result.Summary);
        Assert.Equal(0, result.Summary!.TotalWindows);
        Assert.Equal(0, result.Summary.ConsistencyScore);
        Assert.Empty(result.Windows);
    }

    [Fact]
    public async Task SingleWindow_ProducesOneIsOneOos()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        var strategy = MakeNeverTriggersStrategy();

        // IS=10, OOS=5. Need exactly one pass through the loop.
        // currentStart=Jan1, isSampleEnd=Jan11, oosEnd=Jan16.
        // Next iteration: currentStart=Jan6, isSampleEnd=Jan16 >= end(Jan16), break.
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 16);
        var bars = MakeRisingBars(15, start);

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", start, end)).ReturnsAsync(bars);

        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            start, end, 100_000, inSampleDays: 10, outOfSampleDays: 5);

        var isWindows = result.Windows.Where(w => w.IsInSample).ToList();
        var oosWindows = result.Windows.Where(w => !w.IsInSample).ToList();
        Assert.Single(isWindows);
        Assert.Single(oosWindows);
    }

    [Fact]
    public async Task MultiWindow_SlidesCorrectly()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        var strategy = MakeNeverTriggersStrategy();

        // IS=10, OOS=5. Window slides by OOS=5 each iteration.
        // Start: Jan 1, End: Jan 26 (25 days range).
        // Window 1: currentStart=Jan1, isSampleEnd=Jan11, oosEnd=Jan16. Slide to Jan6.
        // Window 2: currentStart=Jan6, isSampleEnd=Jan16, oosEnd=Jan21. Slide to Jan11.
        // Window 3: currentStart=Jan11, isSampleEnd=Jan21, oosEnd=Jan26. Slide to Jan16.
        // Window 4: currentStart=Jan16, isSampleEnd=Jan26 >= end(Jan26). Break.
        // => 3 IS windows + 3 OOS windows
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 26);
        var bars = MakeRisingBars(25, start);

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", start, end)).ReturnsAsync(bars);

        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            start, end, 100_000, inSampleDays: 10, outOfSampleDays: 5);

        var oosWindows = result.Windows.Where(w => !w.IsInSample).ToList();
        Assert.True(oosWindows.Count >= 2, $"Expected multiple OOS windows, got {oosWindows.Count}");
    }

    [Fact]
    public async Task OosTruncation_WhenEndDateReached()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        var strategy = MakeNeverTriggersStrategy();

        // IS=10, OOS=10. Start=Jan1, End=Jan18.
        // currentStart=Jan1, isSampleEnd=Jan11, oosEnd=Jan21 but clipped to Jan18.
        // Next: currentStart=Jan11, isSampleEnd=Jan21 >= end(Jan18). Break.
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 18);
        var bars = MakeRisingBars(17, start);

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", start, end)).ReturnsAsync(bars);

        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            start, end, 100_000, inSampleDays: 10, outOfSampleDays: 10);

        var oosWindows = result.Windows.Where(w => !w.IsInSample).ToList();
        Assert.Single(oosWindows);
        // The OOS window's EndDate should be clipped to endDate (Jan 18)
        Assert.True(oosWindows[0].EndDate <= end, "OOS EndDate should not exceed the analysis end date");
    }

    [Fact]
    public async Task AllOosProfitable_ConsistencyScoreOne()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        // Strategy that buys on first bar and holds. Force-closed at end of window.
        // With rising prices, the position gains value => positive return.
        var strategy = MakeBuyOnlyStrategy();

        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 26);
        // Rising prices ensure positive returns in each window
        var bars = MakeRisingBars(25, start, startPrice: 100m, dailyIncrease: 2m);

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", start, end)).ReturnsAsync(bars);

        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            start, end, 100_000, inSampleDays: 10, outOfSampleDays: 5);

        var oosWindows = result.Windows.Where(w => !w.IsInSample).ToList();
        Assert.True(oosWindows.Count > 0);
        // With rising prices and buy/sell cycling, all OOS windows should be profitable
        Assert.Equal(1m, result.Summary!.ConsistencyScore);
    }

    [Fact]
    public async Task NoOosProfitable_ConsistencyScoreZero()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        // A strategy that never triggers produces 0% return, which is <= 0, so not profitable
        var strategy = MakeNeverTriggersStrategy();

        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 26);
        var bars = MakeRisingBars(25, start);

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", start, end)).ReturnsAsync(bars);

        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            start, end, 100_000, inSampleDays: 10, outOfSampleDays: 5);

        var oosWindows = result.Windows.Where(w => !w.IsInSample).ToList();
        Assert.True(oosWindows.Count > 0);
        // 0% return is not > 0, so consistency should be 0
        Assert.Equal(0m, result.Summary!.ConsistencyScore);
    }

    [Fact]
    public async Task StrategyNotFound_ThrowsInvalidOperation()
    {
        var (service, strategyService, marketDataService) = CreateSut();

        strategyService.Setup(s => s.GetStrategyAsync("bad-id", "acc-1"))
            .ReturnsAsync((TradingStrategy?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync("bad-id", "acc-1", "user-1", "SPY",
                new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 100_000));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WindowProperties_IsInSampleCorrect()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        var strategy = MakeNeverTriggersStrategy();

        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 16);
        var bars = MakeRisingBars(15, start);

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", start, end)).ReturnsAsync(bars);

        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            start, end, 100_000, inSampleDays: 10, outOfSampleDays: 5);

        // First window should be IS, second should be OOS
        Assert.True(result.Windows.Count >= 2);
        Assert.True(result.Windows[0].IsInSample, "First window should be in-sample");
        Assert.False(result.Windows[1].IsInSample, "Second window should be out-of-sample");
    }

    [Fact]
    public async Task DefaultParameters_252InSample63OutOfSample()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        var strategy = MakeNeverTriggersStrategy();

        // Need IS (252) + at least 1 OOS day = 253+ days in the range
        var start = new DateOnly(2023, 1, 1);
        var end = new DateOnly(2024, 2, 1); // ~396 days
        var bars = MakeRisingBars(396, start);

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", start, end)).ReturnsAsync(bars);

        // Use defaults: inSampleDays=252, outOfSampleDays=63
        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            start, end, 100_000);

        Assert.True(result.Windows.Any(w => w.IsInSample), "Should have at least one IS window");
        Assert.True(result.Windows.Any(w => !w.IsInSample), "Should have at least one OOS window");
    }

    [Fact]
    public async Task Summary_AverageSharpeCalculated()
    {
        var (service, strategyService, marketDataService) = CreateSut();
        // Use a never-triggers strategy: all windows get sharpe = 0
        var strategy = MakeNeverTriggersStrategy();

        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 26);
        var bars = MakeRisingBars(25, start);

        strategyService.Setup(s => s.GetStrategyAsync("str-1", "acc-1")).ReturnsAsync(strategy);
        marketDataService.Setup(m => m.GetHistoricalBarsAsync("SPY", start, end)).ReturnsAsync(bars);

        var result = await service.RunAsync("str-1", "acc-1", "user-1", "SPY",
            start, end, 100_000, inSampleDays: 10, outOfSampleDays: 5);

        // With a never-triggers strategy, all OOS sharpe = 0, so average = 0
        Assert.True(result.Summary!.TotalWindows > 0);
        Assert.Equal(0m, result.Summary.AverageOosSharpe);
        Assert.Equal(0m, result.Summary.AverageOosReturn);
    }
}
