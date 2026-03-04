using MacroSutra.Core.Models;

namespace MacroSutra.Services;

/// <summary>
/// Runs rolling in-sample/out-of-sample backtests (walk-forward analysis).
/// Slides a window across the date range, running the backtest engine for each segment.
/// </summary>
public class WalkForwardService(
    StrategyService strategyService,
    MarketDataService marketDataService,
    BacktestEngine engine)
{
    /// <summary>
    /// Runs a walk-forward analysis.
    /// </summary>
    public async Task<WalkForwardResult> RunAsync(
        string strategyId, string accountId, string userId,
        string symbol, DateOnly startDate, DateOnly endDate,
        decimal initialCapital,
        int inSampleDays = 252, int outOfSampleDays = 63,
        decimal slippageBps = 0, decimal commissionPerTrade = 0)
    {
        var strategy = await strategyService.GetStrategyAsync(strategyId, accountId)
            ?? throw new InvalidOperationException("Strategy not found.");

        // Fetch all bars for the full date range
        var allBars = await marketDataService.GetHistoricalBarsAsync(symbol, startDate, endDate);
        if (allBars.Count == 0)
            return new WalkForwardResult { Summary = new WalkForwardSummary() };

        var result = new WalkForwardResult();
        var currentStart = startDate;

        while (true)
        {
            var isSampleEnd = currentStart.AddDays(inSampleDays);
            // Need at least a full IS window and at least 1 day of OOS
            if (isSampleEnd >= endDate) break;

            var oosEnd = isSampleEnd.AddDays(outOfSampleDays);
            if (oosEnd > endDate) oosEnd = endDate;

            // In-sample window
            var isBars = allBars.Where(b => b.Date >= currentStart && b.Date < isSampleEnd).ToList();
            if (isBars.Count > 0)
            {
                var isResult = engine.Run(strategy, symbol, isBars, initialCapital, slippageBps, commissionPerTrade);
                result.Windows.Add(new WalkForwardWindow
                {
                    StartDate = currentStart,
                    EndDate = isSampleEnd.AddDays(-1),
                    IsInSample = true,
                    TotalReturnPercent = isResult.Metrics?.TotalReturnPercent ?? 0,
                    SharpeRatio = isResult.Metrics?.SharpeRatio ?? 0,
                    MaxDrawdownPercent = isResult.Metrics?.MaxDrawdownPercent ?? 0,
                    TotalTrades = isResult.Metrics?.TotalTrades ?? 0
                });
            }

            // Out-of-sample window
            var oosBars = allBars.Where(b => b.Date >= isSampleEnd && b.Date < oosEnd).ToList();
            if (oosBars.Count > 0)
            {
                var oosResult = engine.Run(strategy, symbol, oosBars, initialCapital, slippageBps, commissionPerTrade);
                result.Windows.Add(new WalkForwardWindow
                {
                    StartDate = isSampleEnd,
                    EndDate = oosEnd,
                    IsInSample = false,
                    TotalReturnPercent = oosResult.Metrics?.TotalReturnPercent ?? 0,
                    SharpeRatio = oosResult.Metrics?.SharpeRatio ?? 0,
                    MaxDrawdownPercent = oosResult.Metrics?.MaxDrawdownPercent ?? 0,
                    TotalTrades = oosResult.Metrics?.TotalTrades ?? 0
                });
            }

            // Slide window forward by OOS period
            currentStart = currentStart.AddDays(outOfSampleDays);
        }

        // Compute summary from OOS windows
        var oosWindows = result.Windows.Where(w => !w.IsInSample).ToList();
        if (oosWindows.Count > 0)
        {
            var profitable = oosWindows.Count(w => w.TotalReturnPercent > 0);
            result.Summary = new WalkForwardSummary
            {
                AverageOosSharpe = oosWindows.Average(w => w.SharpeRatio),
                AverageOosReturn = oosWindows.Average(w => w.TotalReturnPercent),
                ConsistencyScore = (decimal)profitable / oosWindows.Count,
                TotalWindows = oosWindows.Count,
                ProfitableOosWindows = profitable
            };
        }
        else
        {
            result.Summary = new WalkForwardSummary();
        }

        return result;
    }
}
