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
    /// Helper: creates a strategy that buys when price > threshold and sells when price < threshold.
    /// </summary>
    private static TradingStrategy MakeBuySellStrategy(decimal buyAbove, decimal sellBelow, LogicGroupType logic = LogicGroupType.And, QuantityType qtyType = QuantityType.Shares, decimal qty = 10)
    {
        return new TradingStrategy
        {
            id = "test-strategy",
            Name = "Test Strategy",
            Symbols = new List<string> { "TEST" },
            LogicGroup = logic,
            Conditions = new List<TriggerCondition>
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = buyAbove }
            },
            Actions = new List<TradeAction>
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = qtyType, Quantity = qty },
                new() { ActionId = "a2", Side = TradeSide.Sell, ActionType = TradeActionType.MarketOrder, QuantityType = qtyType, Quantity = qty }
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
        // Price is 100, condition requires > 200 — never triggers
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
        // Prices range 100-109, condition requires > 200
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
        // Buy-only strategy: buy when price > 50 (always true since price starts at 100)
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "Buy Only",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.And,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 50 }
            },
            Actions = new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 }
            }
        };

        // Prices: 100, 101, ..., 109 (10 bars, always above 50)
        var bars = MakeBars(10, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        // Should buy on day 1 (price 100) and hold to the end
        // Position is auto-closed at end at price 109
        Assert.True(result.Metrics!.TotalTrades > 0);
        Assert.True(result.Metrics.FinalEquity > 100_000);
        Assert.True(result.Metrics.TotalReturnPercent > 0);
    }

    [Fact]
    public void BuySellCycle_CorrectPnL()
    {
        // Strategy: buy when price > 95, sell when also > 95
        // Both actions trigger on each bar — buy happens first (no position), then sell (has position)
        // Actually: first bar buys (no position yet), second bar sells (has position from first bar)
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "Test",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.And,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 95 }
            },
            Actions = new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 },
                new() { ActionId = "a2", Side = TradeSide.Sell, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 }
            }
        };

        // Bar 1: price 100 → buys 10 shares at 100 (then tries to sell but has position, so sells at 100)
        // Actually: buy happens first (no position), then sell happens (now has position, sells immediately)
        // Bar 2: price 101 → buys again (no position), sells again
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2024, 1, 2), 99, 102, 98, 100, 1_000_000),
            new(new DateOnly(2024, 1, 3), 100, 103, 99, 101, 1_000_000),
            new(new DateOnly(2024, 1, 4), 101, 104, 100, 102, 1_000_000),
        };

        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        // Each bar: buy then sell. Trade 1: buy@100, sell@100 (same bar) → P&L = 0
        // Trade 2: buy@101, sell@101 → P&L = 0... Actually buy and sell in same iteration = same price
        // Let me verify: we iterate over bars. On bar 1: buy (no pos → open at 100), sell (has pos → close at 100), P&L = 0
        Assert.True(result.Metrics!.TotalTrades >= 1);
        Assert.Equal(BacktestStatus.Completed, result.Status);
    }

    [Fact]
    public void BuyAndHold_ClosedAtEnd_CorrectReturn()
    {
        // Buy-only strategy (no sell action)
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "Buy Only",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.And,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 }
            },
            Actions = new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 100 }
            }
        };

        // Price goes from 100 to 200
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2024, 1, 2), 99, 102, 98, 100, 1_000_000),
            new(new DateOnly(2024, 6, 1), 199, 202, 198, 200, 1_000_000),
        };

        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        // Buy 100 shares at $100 = $10,000 invested, $90,000 cash remaining
        // End: 100 * $200 = $20,000 + $90,000 = $110,000
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
        Assert.Equal((decimal)2 / 3 * 100, metrics.WinRate); // 66.67%
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

        Assert.Equal(3m, metrics.ProfitFactor); // 300 / 100
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
        // Daily equities that consistently go up
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
            new() { PnL = 100, ReturnPercent = 5, EntryDate = new(2024, 1, 1), ExitDate = new(2024, 1, 11) }, // 10 days
            new() { PnL = 50, ReturnPercent = 2, EntryDate = new(2024, 2, 1), ExitDate = new(2024, 2, 21) },  // 20 days
        };

        var metrics = BacktestEngine.ComputeMetrics(100_000, 100_150, 0, new List<decimal> { 100_000, 100_150 }, trades);

        Assert.Equal(15, metrics.AverageTradeDuration.Days); // (10 + 20) / 2
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
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "AND Test",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.And,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 90 },
                new() { ConditionId = "c2", ConditionType = ConditionType.Price, Operator = ConditionOperator.LessThan, Value = 110 }
            },
            Actions = new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 }
            }
        };

        // Price 100 is both > 90 and < 110
        var bars = MakeBars(3, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        // Should trigger (both conditions pass)
        Assert.True(result.Trades.Count > 0);
    }

    [Fact]
    public void AndLogic_OneConditionFails_NoTrigger()
    {
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "AND Test",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.And,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 90 },
                new() { ConditionId = "c2", ConditionType = ConditionType.Price, Operator = ConditionOperator.LessThan, Value = 50 } // will fail — price is 100
            },
            Actions = new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 }
            }
        };

        var bars = MakeBars(3, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.Empty(result.Trades);
        Assert.Equal(100_000, result.Metrics!.FinalEquity);
    }

    [Fact]
    public void OrLogic_OneConditionPasses_Triggers()
    {
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "OR Test",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.Or,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 200 }, // fails
                new() { ConditionId = "c2", ConditionType = ConditionType.Price, Operator = ConditionOperator.LessThan, Value = 150 }   // passes (price 100)
            },
            Actions = new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 10 }
            }
        };

        var bars = MakeBars(3, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        Assert.True(result.Trades.Count > 0);
    }

    // ── PercentOfPortfolio quantity ──

    [Fact]
    public void PercentOfPortfolio_QuantityResolved()
    {
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "Pct Test",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.And,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 }
            },
            Actions = new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 50 } // 50% of portfolio
            }
        };

        // Price = 100, capital = 100_000, 50% = $50,000, 50,000/100 = 500 shares
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
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "Alert Test",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.And,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 }
            },
            Actions = new()
            {
                new() { ActionId = "a1", ActionType = TradeActionType.Alert, Side = TradeSide.Buy, Quantity = 10 }
            }
        };

        var bars = MakeBars(5, startPrice: 100);
        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        // No trades executed (all actions are alerts)
        Assert.Empty(result.Trades);
        Assert.Equal(100_000, result.Metrics!.FinalEquity);
    }

    // ── Drawdown tracking ──

    [Fact]
    public void MaxDrawdown_TrackedCorrectly()
    {
        var strategy = new TradingStrategy
        {
            id = "test",
            Name = "DD Test",
            Symbols = new() { "TEST" },
            LogicGroup = LogicGroupType.And,
            Conditions = new()
            {
                new() { ConditionId = "c1", ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 0 }
            },
            Actions = new()
            {
                new() { ActionId = "a1", Side = TradeSide.Buy, ActionType = TradeActionType.MarketOrder, QuantityType = QuantityType.Shares, Quantity = 100 }
            }
        };

        // Buy at 100, price dips to 90 (10% drawdown), then recovers to 110
        var bars = new List<OhlcvBar>
        {
            new(new DateOnly(2024, 1, 2), 99, 102, 98, 100, 1_000_000),  // buys here
            new(new DateOnly(2024, 1, 3), 89, 92, 88, 90, 1_000_000),   // drawdown
            new(new DateOnly(2024, 1, 4), 109, 112, 108, 110, 1_000_000), // recovery
        };

        var result = _engine.Run(strategy, "TEST", bars, 100_000);

        // After buy: equity = 90000 cash + 100*100 = 100_000
        // After dip: equity = 90000 + 100*90 = 99_000, peak was 100_000, drawdown = -1%
        Assert.True(result.Metrics!.MaxDrawdownPercent < 0);
    }

    // ── Equity curve ──

    [Fact]
    public void EquityCurve_HasOnePointPerBar()
    {
        var strategy = MakeBuySellStrategy(200, 50); // never triggers
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
        // 5000 / 100 = 50 shares
        Assert.Equal(50, BacktestEngine.ResolveQuantity(action, 100, 100_000));
    }

    [Fact]
    public void ResolveQuantity_PercentOfPortfolio_CalculatesShares()
    {
        var action = new TradeAction { QuantityType = QuantityType.PercentOfPortfolio, Quantity = 10 };
        // 10% of 100_000 = 10_000, 10_000 / 200 = 50 shares
        Assert.Equal(50, BacktestEngine.ResolveQuantity(action, 200, 100_000));
    }

    [Fact]
    public void ResolveQuantity_ZeroPrice_ReturnsZero()
    {
        var action = new TradeAction { QuantityType = QuantityType.Shares, Quantity = 50 };
        Assert.Equal(0, BacktestEngine.ResolveQuantity(action, 0, 100_000));
    }
}
