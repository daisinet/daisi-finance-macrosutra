using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// Orchestrates backtests: loads strategy, fetches historical data,
/// runs the engine, and persists results to Cosmos.
/// </summary>
public class BacktestService(
    MacroSutraCosmo cosmo,
    StrategyService strategyService,
    MarketDataService marketDataService,
    BacktestEngine engine)
{
    /// <summary>
    /// Creates and runs a backtest, persisting the result to Cosmos.
    /// </summary>
    public virtual async Task<BacktestResult> CreateAndRunBacktestAsync(
        string strategyId, string accountId, string userId,
        string symbol, DateOnly from, DateOnly to, decimal initialCapital,
        decimal slippageBps = 0, decimal commissionPerTrade = 0,
        string? timeFrame = null)
    {
        // Load strategy
        var strategy = await strategyService.GetStrategyAsync(strategyId, accountId)
            ?? throw new InvalidOperationException("Strategy not found.");

        // Create initial document
        var result = new BacktestResult
        {
            AccountId = accountId,
            UserId = userId,
            StrategyId = strategyId,
            StrategyName = strategy.Name,
            Symbol = symbol,
            StartDate = from,
            EndDate = to,
            InitialCapital = initialCapital,
            SlippageBps = slippageBps,
            CommissionPerTrade = commissionPerTrade,
            TimeFrame = timeFrame,
            Status = BacktestStatus.Running
        };
        result = await cosmo.CreateBacktestAsync(result);

        try
        {
            // Fetch historical bars for each interval used by trigger groups
            var intervals = strategy.TriggerGroups
                .Select(tg => tg.Interval)
                .Distinct()
                .ToList();
            if (intervals.Count == 0)
                intervals.Add(timeFrame != null ? ParseTimeFrame(timeFrame) : Core.Enums.BarTimeFrame.Day);

            var barsByInterval = new Dictionary<Core.Enums.BarTimeFrame, List<OhlcvBar>>();
            foreach (var interval in intervals)
            {
                barsByInterval[interval] = await marketDataService.GetHistoricalBarsAsync(symbol, from, to, interval);
            }

            // Run simulation
            var engineResult = engine.Run(strategy, symbol, barsByInterval, initialCapital, slippageBps, commissionPerTrade);

            // Copy engine results to persisted document
            result.Metrics = engineResult.Metrics;
            result.EquityCurve = engineResult.EquityCurve;
            result.Trades = engineResult.Trades;
            result.Status = BacktestStatus.Completed;
            result.CompletedUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Status = BacktestStatus.Failed;
            result.ErrorMessage = ex.Message;
            result.CompletedUtc = DateTime.UtcNow;
        }

        await cosmo.UpdateBacktestAsync(result);
        return result;
    }

    public virtual async Task<BacktestResult?> GetBacktestAsync(string id, string accountId)
    {
        return await cosmo.GetBacktestAsync(id, accountId);
    }

    public virtual async Task<List<BacktestResult>> GetBacktestsAsync(string accountId, string? strategyId = null)
    {
        return await cosmo.GetBacktestsAsync(accountId, strategyId);
    }

    public virtual async Task DeleteBacktestAsync(string id, string accountId)
    {
        await cosmo.DeleteBacktestAsync(id, accountId);
    }

    internal static Core.Enums.BarTimeFrame ParseTimeFrame(string? timeFrame) => timeFrame switch
    {
        "Hour" => Core.Enums.BarTimeFrame.Hour,
        "FifteenMinutes" => Core.Enums.BarTimeFrame.FifteenMinutes,
        "FiveMinutes" => Core.Enums.BarTimeFrame.FiveMinutes,
        "OneMinute" => Core.Enums.BarTimeFrame.OneMinute,
        _ => Core.Enums.BarTimeFrame.Day
    };
}
