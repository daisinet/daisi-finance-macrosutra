using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Services;

/// <summary>
/// Pure computation service that runs a strategy against historical OHLCV bars.
/// No Cosmos or external dependencies — takes inputs, returns results.
/// </summary>
public class BacktestEngine(ConditionEvaluator evaluator)
{
    /// <summary>
    /// Runs a backtest simulation for a strategy against historical bars.
    /// Single-interval overload — all trigger groups evaluate against the same bars.
    /// </summary>
    public BacktestResult Run(TradingStrategy strategy, string symbol,
        List<OhlcvBar> bars, decimal initialCapital,
        decimal slippageBps = 0, decimal commissionPerTrade = 0)
    {
        var barsByInterval = new Dictionary<BarTimeFrame, List<OhlcvBar>>();
        foreach (var tg in strategy.TriggerGroups)
            barsByInterval.TryAdd(tg.Interval, bars);
        if (barsByInterval.Count == 0)
            barsByInterval[BarTimeFrame.Day] = bars;

        return Run(strategy, symbol, barsByInterval, initialCapital, slippageBps, commissionPerTrade);
    }

    /// <summary>
    /// Runs a backtest simulation with per-interval bar data.
    /// Each trigger group evaluates against bars at its configured interval.
    /// The finest-grained bars drive the main timeline for equity tracking.
    /// </summary>
    public BacktestResult Run(TradingStrategy strategy, string symbol,
        Dictionary<BarTimeFrame, List<OhlcvBar>> barsByInterval, decimal initialCapital,
        decimal slippageBps = 0, decimal commissionPerTrade = 0)
    {
        var result = new BacktestResult
        {
            StrategyId = strategy.id,
            StrategyName = strategy.Name,
            Symbol = symbol,
            InitialCapital = initialCapital,
            SlippageBps = slippageBps,
            CommissionPerTrade = commissionPerTrade,
            Status = BacktestStatus.Running
        };

        // Use the finest-grained bars as the main timeline
        var timelineBars = GetFinestBars(barsByInterval);
        if (timelineBars.Count == 0)
        {
            result.Status = BacktestStatus.Completed;
            result.CompletedUtc = DateTime.UtcNow;
            result.Metrics = new BacktestMetrics { FinalEquity = initialCapital };
            return result;
        }

        // Build rolling price windows per interval
        var closePricesByInterval = new Dictionary<BarTimeFrame, List<decimal>>();
        var barIndexByInterval = new Dictionary<BarTimeFrame, int>();
        foreach (var interval in barsByInterval.Keys)
        {
            closePricesByInterval[interval] = new List<decimal>();
            barIndexByInterval[interval] = 0;
        }

        var cash = initialCapital;
        var peakEquity = initialCapital;
        var maxDrawdownPercent = 0m;
        var previousValues = new Dictionary<string, decimal>();
        var dailyEquities = new List<decimal> { initialCapital };

        // Current open position (null = flat)
        SimulatedTrade? openPosition = null;

        for (int i = 0; i < timelineBars.Count; i++)
        {
            var bar = timelineBars[i];

            // Advance each interval's price window up to the current timestamp
            foreach (var (interval, intervalBars) in barsByInterval)
            {
                var idx = barIndexByInterval[interval];
                var prices = closePricesByInterval[interval];
                while (idx < intervalBars.Count && intervalBars[idx].Timestamp <= bar.Timestamp)
                {
                    prices.Add(intervalBars[idx].Close);
                    idx++;
                }
                barIndexByInterval[interval] = idx;
            }

            // Compute change % from previous timeline bar
            decimal dailyChangePercent = 0;
            if (i > 0 && timelineBars[i - 1].Close > 0)
                dailyChangePercent = (bar.Close - timelineBars[i - 1].Close) / timelineBars[i - 1].Close * 100;

            // Synthesize a MarketSnapshot from this bar
            var snapshot = new MarketSnapshot
            {
                Symbol = symbol,
                Price = bar.Close,
                Volume = bar.Volume,
                DailyHigh = bar.High,
                DailyLow = bar.Low,
                DailyChangePercent = dailyChangePercent,
                PreviousClose = i > 0 ? timelineBars[i - 1].Close : bar.Open,
                Timestamp = bar.Timestamp
            };

            // Evaluate each trigger group against its own interval's price history
            var firedActions = new List<(string reason, List<TradeAction> actions)>();

            foreach (var tg in strategy.TriggerGroups)
            {
                var prices = closePricesByInterval.TryGetValue(tg.Interval, out var p) ? p : closePricesByInterval.Values.First();
                var priceSlice = prices.Count > 50
                    ? prices.Skip(prices.Count - 50).ToArray()
                    : prices.ToArray();

                var (groupTriggered, groupResults) = evaluator.EvaluateGroup(
                    tg.Conditions, snapshot, priceSlice, previousValues);
                if (groupTriggered)
                {
                    var reason = string.Join(", ", groupResults.Where(r => r.passed).Select(r => r.conditionId));
                    firedActions.Add((reason, tg.Actions));
                }
            }

            foreach (var (firedConditions, actions) in firedActions)
            {
                foreach (var action in actions)
                {
                    // Skip alert actions in backtest
                    if (action.ActionType == TradeActionType.Alert)
                        continue;

                    if (action.Side == TradeSide.Buy && openPosition == null)
                    {
                        // Open a long position (slippage: pay more on buy)
                        var fillPrice = bar.Close * (1 + slippageBps / 10000m);
                        var equity = cash; // no position, cash = equity
                        var quantity = ResolveQuantity(action, fillPrice, equity);
                        var totalCost = quantity * fillPrice + commissionPerTrade;
                        if (quantity > 0 && totalCost <= cash)
                        {
                            cash -= totalCost;
                            openPosition = new SimulatedTrade
                            {
                                Symbol = symbol,
                                Side = TradeSide.Buy,
                                Quantity = quantity,
                                EntryPrice = fillPrice,
                                EntryDate = bar.Date,
                                TriggerReason = firedConditions
                            };
                        }
                    }
                    else if (action.Side == TradeSide.Sell && openPosition != null)
                    {
                        // Close the position (slippage: receive less on sell)
                        var fillPrice = bar.Close * (1 - slippageBps / 10000m);
                        var proceeds = openPosition.Quantity * fillPrice - commissionPerTrade;
                        cash += proceeds;
                        var costBasis = openPosition.Quantity * openPosition.EntryPrice + commissionPerTrade;
                        var pnl = proceeds - costBasis;
                        var returnPct = costBasis > 0 ? pnl / costBasis * 100 : 0;

                        openPosition.ExitPrice = fillPrice;
                        openPosition.ExitDate = bar.Date;
                        openPosition.PnL = pnl;
                        openPosition.ReturnPercent = returnPct;
                        result.Trades.Add(openPosition);
                        openPosition = null;
                    }
                }
            }

            // Calculate equity (cash + position market value)
            var equity2 = cash + (openPosition?.Quantity ?? 0) * bar.Close;
            dailyEquities.Add(equity2);

            // Track peak and drawdown
            if (equity2 > peakEquity)
                peakEquity = equity2;

            var drawdown = peakEquity > 0 ? (equity2 - peakEquity) / peakEquity * 100 : 0;
            if (drawdown < maxDrawdownPercent)
                maxDrawdownPercent = drawdown;

            result.EquityCurve.Add(new EquityCurvePoint
            {
                Date = bar.Date,
                Timestamp = bar.Timestamp,
                Equity = equity2,
                Drawdown = drawdown
            });
        }

        // If still holding a position at the end, close it at last bar's close
        if (openPosition != null)
        {
            var lastBar = timelineBars[^1];
            var exitPrice = lastBar.Close * (1 - slippageBps / 10000m);
            var proceeds = openPosition.Quantity * exitPrice - commissionPerTrade;
            cash += proceeds;
            var costBasis = openPosition.Quantity * openPosition.EntryPrice + commissionPerTrade;
            var pnl = proceeds - costBasis;
            var returnPct = costBasis > 0 ? pnl / costBasis * 100 : 0;

            openPosition.ExitPrice = exitPrice;
            openPosition.ExitDate = lastBar.Date;
            openPosition.PnL = pnl;
            openPosition.ReturnPercent = returnPct;
            result.Trades.Add(openPosition);

            // Update the last equity curve point to reflect forced close
            if (result.EquityCurve.Count > 0)
            {
                result.EquityCurve[^1].Equity = cash;
            }
        }

        // Compute metrics
        var finalEquity = cash;
        result.Metrics = ComputeMetrics(initialCapital, finalEquity, maxDrawdownPercent, dailyEquities, result.Trades);
        result.Status = BacktestStatus.Completed;
        result.CompletedUtc = DateTime.UtcNow;

        return result;
    }

    /// <summary>
    /// Returns the finest-grained (most bars) list from the dictionary.
    /// </summary>
    private static List<OhlcvBar> GetFinestBars(Dictionary<BarTimeFrame, List<OhlcvBar>> barsByInterval)
    {
        if (barsByInterval.Count == 0) return [];
        return barsByInterval.OrderByDescending(kvp => kvp.Value.Count).First().Value;
    }

    internal static BacktestMetrics ComputeMetrics(
        decimal initialCapital, decimal finalEquity, decimal maxDrawdownPercent,
        List<decimal> dailyEquities, List<SimulatedTrade> trades)
    {
        var metrics = new BacktestMetrics
        {
            FinalEquity = finalEquity,
            TotalReturnPercent = initialCapital > 0 ? (finalEquity - initialCapital) / initialCapital * 100 : 0,
            MaxDrawdownPercent = maxDrawdownPercent,
            TotalTrades = trades.Count
        };

        if (trades.Count > 0)
        {
            var winners = trades.Where(t => t.PnL > 0).ToList();
            var losers = trades.Where(t => t.PnL <= 0).ToList();

            metrics.WinningTrades = winners.Count;
            metrics.LosingTrades = losers.Count;
            metrics.WinRate = trades.Count > 0 ? (decimal)winners.Count / trades.Count * 100 : 0;

            var grossProfit = winners.Sum(t => t.PnL ?? 0);
            var grossLoss = Math.Abs(losers.Sum(t => t.PnL ?? 0));
            metrics.ProfitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? 9999.99m : 0;

            var returns = trades.Where(t => t.ReturnPercent.HasValue).Select(t => t.ReturnPercent!.Value).ToList();
            metrics.AverageTradeReturnPercent = returns.Count > 0 ? returns.Average() : 0;
            metrics.BestTradePercent = returns.Count > 0 ? returns.Max() : 0;
            metrics.WorstTradePercent = returns.Count > 0 ? returns.Min() : 0;

            var durations = trades
                .Where(t => t.ExitDate.HasValue)
                .Select(t => t.ExitDate!.Value.ToDateTime(TimeOnly.MinValue) - t.EntryDate.ToDateTime(TimeOnly.MinValue))
                .ToList();
            metrics.AverageTradeDuration = durations.Count > 0
                ? TimeSpan.FromTicks((long)durations.Average(d => d.Ticks))
                : TimeSpan.Zero;
        }

        // Sharpe ratio from daily equity returns (annualized)
        if (dailyEquities.Count > 1)
        {
            var dailyReturns = new List<decimal>();
            for (int i = 1; i < dailyEquities.Count; i++)
            {
                if (dailyEquities[i - 1] > 0)
                    dailyReturns.Add((dailyEquities[i] - dailyEquities[i - 1]) / dailyEquities[i - 1]);
            }

            if (dailyReturns.Count > 1)
            {
                var avgReturn = dailyReturns.Average();
                var variance = dailyReturns.Sum(r => (r - avgReturn) * (r - avgReturn)) / (dailyReturns.Count - 1);
                var stdDev = (decimal)Math.Sqrt((double)variance);

                metrics.SharpeRatio = stdDev > 0
                    ? Math.Round(avgReturn / stdDev * (decimal)Math.Sqrt(252), 2)
                    : 0;
            }
        }

        return metrics;
    }

    /// <summary>
    /// Resolves trade quantity (mirrors TradeExecutionService.ResolveQuantity logic).
    /// </summary>
    internal static decimal ResolveQuantity(TradeAction action, decimal currentPrice, decimal accountBalance)
    {
        if (currentPrice <= 0) return 0;

        return action.QuantityType switch
        {
            QuantityType.Shares => action.Quantity,
            QuantityType.DollarAmount => Math.Floor(action.Quantity / currentPrice * 100) / 100,
            QuantityType.PercentOfPortfolio => Math.Floor(accountBalance * action.Quantity / 100 / currentPrice * 100) / 100,
            _ => action.Quantity
        };
    }
}
