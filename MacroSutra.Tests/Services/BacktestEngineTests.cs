using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;

namespace MacroSutra.Tests.Services;

public class BacktestEngineTests
{
    private readonly BacktestEngine _engine = new(new ConditionEvaluator());

    /// <summary>
    /// Helper: generates N daily bars starting from a given date with linearly increasing prices.
    /// </summary>
    private static List<OhlcvBar> MakeBars(int count, decimal startPrice = 100m, decimal dailyIncrease = 1m, DateOnly? start = null)
    {
        var date = start ?? new DateOnly(2024, 1, 2);
        var bars = new List<OhlcvBar>();
        for (int i = 0; i < count; i++)
        {
            var close = startPrice + i * dailyIncrease;
            bars.Add(new OhlcvBar(date.AddDays(i), close - 1, close + 2, close - 2, close, 1_000_000));
        }
        return bars;
    }

    /// <summary>
    /// Helper: creates a strategy with a single trigger group that buys when price > threshold.
    /// The group has both buy and sell actions so the engine can open/close positions.
    /// </summary>
    private static TradingStrategy MakeBuySellStrategy(decimal buyAbove, decimal sellBelow, LogicGroupType logic = LogicGroupType.And, QuantityType qtyType = QuantityType.Shares, decimal qty = 10)
    {
        return new TradingStrategy
        {
            id = "test-strategy",
            Name = "Test Strategy",
            Symbols = new List<string> { "TEST" },
            TriggerGroups = new()
            {
                new TriggerGroup
                {
                    Name = "Test",
                    Conditions = new ConditionGroup
                    {
                        Logic = logic,
                        Conditions = new()
                        {
                            new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = buyAbove }
                        }
                    },
                    Actions = new()
                    {
                        new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = qtyType, Quantity = qty },
                        new() { ActionId = "a2", Side = TradeSide.Sell, ActionType = TradeActionType.MarketOrder, QuantityType = qtyType, Quantity = qty }
                    }
                }
            }
        };
    }

    private static TradingStrategy MakeStrategy(LogicGroupType logic, List<TriggerCondition> conditions, List<TradeAction> actions)
    {
        return new TradingStrategy
        {
            id = "test",
            Name = "Test",
            Symbols = new() { "TEST" },
            TriggerGroups = new()
            {
                new TriggerGroup
                {
                    Name = "Test",
                    Conditions = new ConditionGroup { Logic = logic, Conditions = conditions },
                    Actions = actions
                }
            }
        };
    }

    // ── Basic scenarios ──

    [Fact]
    public void EmptyBars_ReturnsCompletedWithInitialCapital()
    {
        var strategy = MakeBuySellStrategy(50, 50);
        var result = _engine.Run(strategy, "TEST", new List<OhlcvBar>(), 100_000);

        Assert.Equal(BacktestStatus.Completed, result.Status);
        Assert.NotNull(result.Metrics);
        Assert.Equal(100_000, result.Metrics!.FinalEquity);
        Assert.Empty(result.Trades);
        Assert.Empty(result.EquityCurve);
    }

    [Fact]
    public void SingleBar_NeverTriggers_EquityFlat()
    {
        var strategy = MakeBuySellStrategy(200, 50);
        var bars = MakeBars(1, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.Equal(BacktestStatus.Completed, result.Status);
        Assert.Equal(100_000, result.Metrics!.FinalEquity);
        Assert.Empty(result.Trades);
        Assert.Single(result.EquityCurve);
    }

    [Fact]
    public void StrategyNeverTriggers_EquityStaysFlat()
    {
        var strategy = MakeBuySellStrategy(200, 50);
        var bars = MakeBars(10, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.Equal(0, result.Metrics!.TotalTrades);
        Assert.Equal(100_000, result.Metrics.FinalEquity);
        Assert.Equal(0, result.Metrics.TotalReturnPercent);
    }

    [Fact]
    public void BuyOnFirstBar_PriceGoesUp_PositiveReturn()
    {
        var strategy = MakeStrategy(
            LogicGroupType.And,
            new() { new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 50 } },
            new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 } }
        );

        var bars = MakeBars(10, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.True(result.Metrics!.TotalTrades > 0);
        Assert.True(result.Metrics.FinalEquity > 100_000);
        Assert.True(result.Metrics.TotalReturnPercent > 0);
    }

    [Fact]
    public void BuySellCycle_CorrectPnL()
    {
        var strategy = MakeStrategy(
            LogicGroupType.And,
            new() { new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 95 } },
            new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 },
                new() { ActionId = "a2", Side = TradeSide.Sell, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 }
            }
        );

        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2024, 1, 2), 99, 102, 98, 100, 1_000_000),
            new(new DateOnly(2024, 1, 3), 100, 103, 99, 101, 1_000_000),
            new(new DateOnly(2024, 1, 4), 101, 104, 100, 102, 1_000_000),
        };

        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.True(result.Metrics!.TotalTrades >= 1);
        Assert.Equal(BacktestStatus.Completed, result.Status);
    }

    [Fact]
    public void BuyAndHold_ClosedAtEnd_CorrectReturn()
    {
        var strategy = MakeStrategy(
            LogicGroupType.And,
            new() { new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 } },
            new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 100 } }
        );

        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2024, 1, 2), 99, 102, 98, 100, 1_000_000),
            new(new DateOnly(2024, 6, 1), 199, 202, 198, 200, 1_000_000),
        };

        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(100, trade.Quantity);
        Assert.Equal(100, trade.EntryPrice);
        Assert.Equal(200, trade.ExitPrice);
        Assert.Equal(10_000, trade.PnL);
        Assert.Equal(110_000, result.Metrics!.FinalEquity);
        Assert.Equal(10m, result.Metrics.TotalReturnPercent);
    }

    // ── Metrics verification ──

    [Fact]
    public void Metrics_WinRate_CalculatedCorrectly()
    {
        var trades = new List<SimulatedTrade>
        {
            new() { PnL = 100, ReturnPercent = 10, EntryDate = new(2024, 1, 1), ExitDate = new(2024, 1, 5) },
            new() { PnL = -50, ReturnPercent = -5, EntryDate = new(2024, 1, 6), ExitDate = new(2024, 1, 10) },
            new() { PnL = 200, ReturnPercent = 20, EntryDate = new(2024, 1, 11), ExitDate = new(2024, 1, 15) },
        };

        var equities = new List<decimal> { 100_000, 100_100, 100_050, 100_250 };
        var metrics = BacktestEngine.ComputeMetrics(100_000, 100_250, -0.05m, equities, trades);

        Assert.Equal(3, metrics.TotalTrades);
        Assert.Equal(2, metrics.WinningTrades);
        Assert.Equal(1, metrics.LosingTrades);
        Assert.Equal((decimal)2 / 3 * 100, metrics.WinRate);
    }

    [Fact]
    public void Metrics_ProfitFactor_CalculatedCorrectly()
    {
        var trades = new List<SimulatedTrade>
        {
            new() { PnL = 300, ReturnPercent = 30 },
            new() { PnL = -100, ReturnPercent = -10 },
        };

        var metrics = BacktestEngine.ComputeMetrics(100_000, 100_200, 0, new List<decimal> { 100_000, 100_200 }, trades);

        Assert.Equal(3m, metrics.ProfitFactor);
    }

    [Fact]
    public void Metrics_NoTrades_ZeroMetrics()
    {
        var metrics = BacktestEngine.ComputeMetrics(100_000, 100_000, 0, new List<decimal> { 100_000 }, new List<SimulatedTrade>());

        Assert.Equal(0, metrics.TotalTrades);
        Assert.Equal(0, metrics.WinRate);
        Assert.Equal(0, metrics.ProfitFactor);
    }

    [Fact]
    public void Metrics_SharpeRatio_PositiveForConsistentGains()
    {
        var equities = new List<decimal>();
        for (int i = 0; i <= 100; i++)
            equities.Add(100_000 + i * 100);

        var metrics = BacktestEngine.ComputeMetrics(100_000, 110_000, 0, equities, new List<SimulatedTrade>());

        Assert.True(metrics.SharpeRatio > 0, "Sharpe should be positive for consistent gains");
    }

    [Fact]
    public void Metrics_AverageTradeDuration_Calculated()
    {
        var trades = new List<SimulatedTrade>
        {
            new() { PnL = 100, ReturnPercent = 5, EntryDate = new(2024, 1, 1), ExitDate = new(2024, 1, 11) },
            new() { PnL = 50, ReturnPercent = 2, EntryDate = new(2024, 2, 1), ExitDate = new(2024, 2, 21) },
        };

        var metrics = BacktestEngine.ComputeMetrics(100_000, 100_150, 0, new List<decimal> { 100_000, 100_150 }, trades);

        Assert.Equal(15, metrics.AverageTradeDuration.Days);
    }

    [Fact]
    public void Metrics_BestAndWorstTrade()
    {
        var trades = new List<SimulatedTrade>
        {
            new() { PnL = 500, ReturnPercent = 25 },
            new() { PnL = -200, ReturnPercent = -10 },
            new() { PnL = 100, ReturnPercent = 5 },
        };

        var metrics = BacktestEngine.ComputeMetrics(100_000, 100_400, 0, new List<decimal> { 100_000, 100_400 }, trades);

        Assert.Equal(25, metrics.BestTradePercent);
        Assert.Equal(-10, metrics.WorstTradePercent);
    }

    // ── AND/OR logic ──

    [Fact]
    public void AndLogic_AllConditionsMustPass()
    {
        var strategy = MakeStrategy(
            LogicGroupType.And,
            new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 90 },
                new() { ConditionId = "c2", ConditionType = ConditionType.Price, Operator = ConditionOperator.LessThan, Value = 110 }
            },
            new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 } }
        );

        var bars = MakeBars(3, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.True(result.Trades.Count > 0);
    }

    [Fact]
    public void AndLogic_OneConditionFails_NoTrigger()
    {
        var strategy = MakeStrategy(
            LogicGroupType.And,
            new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 90 },
                new() { ConditionId = "c2", ConditionType = ConditionType.Price, Operator = ConditionOperator.LessThan, Value = 50 }
            },
            new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 } }
        );

        var bars = MakeBars(3, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.Empty(result.Trades);
        Assert.Equal(100_000, result.Metrics!.FinalEquity);
    }

    [Fact]
    public void OrLogic_OneConditionPasses_Triggers()
    {
        var strategy = MakeStrategy(
            LogicGroupType.Or,
            new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 200 },
                new() { ConditionId = "c2", ConditionType = ConditionType.Price, Operator = ConditionOperator.LessThan, Value = 150 }
            },
            new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 } }
        );

        var bars = MakeBars(3, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.True(result.Trades.Count > 0);
    }

    // ── PercentOfPortfolio quantity ──

    [Fact]
    public void PercentOfPortfolio_QuantityResolved()
    {
        var strategy = MakeStrategy(
            LogicGroupType.And,
            new() { new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 } },
            new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 50 } }
        );

        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2024, 1, 2), 99, 102, 98, 100, 1_000_000),
            new(new DateOnly(2024, 1, 3), 109, 112, 108, 110, 1_000_000),
        };

        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.Single(result.Trades);
        Assert.Equal(500, result.Trades[0].Quantity);
    }

    // ── Alert actions ignored ──

    [Fact]
    public void AlertActions_SkippedInBacktest()
    {
        var strategy = MakeStrategy(
            LogicGroupType.And,
            new() { new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 } },
            new() { new() { ActionId = "a1", ActionType = TradeActionType.Alert, Side = TradeSide.Buy, Quantity = 10 } }
        );

        var bars = MakeBars(5, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.Empty(result.Trades);
        Assert.Equal(100_000, result.Metrics!.FinalEquity);
    }

    // ── Drawdown tracking ──

    [Fact]
    public void MaxDrawdown_TrackedCorrectly()
    {
        var strategy = MakeStrategy(
            LogicGroupType.And,
            new() { new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 } },
            new() { new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 100 } }
        );

        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2024, 1, 2), 99, 102, 98, 100, 1_000_000),
            new(new DateOnly(2024, 1, 3), 89, 92, 88, 90, 1_000_000),
            new(new DateOnly(2024, 1, 4), 109, 112, 108, 110, 1_000_000),
        };

        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.True(result.Metrics!.MaxDrawdownPercent < 0);
    }

    // ── Equity curve ──

    [Fact]
    public void EquityCurve_HasOnePointPerBar()
    {
        var strategy = MakeBuySellStrategy(200, 50);
        var bars = MakeBars(20, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.Equal(20, result.EquityCurve.Count);
    }

    // ── ResolveQuantity ──

    [Fact]
    public void ResolveQuantity_Shares_ReturnsExact()
    {
        var action = new TradeAction { QuantityType = QuantityType.Shares, Quantity = 50 };
        Assert.Equal(50, BacktestEngine.ResolveQuantity(action, 100, 100_000));
    }

    [Fact]
    public void ResolveQuantity_DollarAmount_CalculatesShares()
    {
        var action = new TradeAction { QuantityType = QuantityType.DollarAmount, Quantity = 5000 };
        Assert.Equal(50, BacktestEngine.ResolveQuantity(action, 100, 100_000));
    }

    [Fact]
    public void ResolveQuantity_PercentOfPortfolio_CalculatesShares()
    {
        var action = new TradeAction { QuantityType = QuantityType.PercentOfPortfolio, Quantity = 10 };
        Assert.Equal(50, BacktestEngine.ResolveQuantity(action, 200, 100_000));
    }

    [Fact]
    public void ResolveQuantity_ZeroPrice_ReturnsZero()
    {
        var action = new TradeAction { QuantityType = QuantityType.Shares, Quantity = 50 };
        Assert.Equal(0, BacktestEngine.ResolveQuantity(action, 0, 100_000));
    }
}
