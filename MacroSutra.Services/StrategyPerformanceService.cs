using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// Tracks strategy trigger events and computes performance summaries.
/// </summary>
public class StrategyPerformanceService(MacroSutraCosmo cosmo)
{
    /// <summary>
    /// Records a new strategy trigger event.
    /// </summary>
    public virtual async Task<StrategyTriggerRecord> RecordTriggerAsync(
        string accountId, string strategyId, string symbol,
        List<Trade> trades)
    {
        var record = new StrategyTriggerRecord
        {
            AccountId = accountId,
            StrategyId = strategyId,
            Symbol = symbol,
            TriggeredUtc = DateTime.UtcNow,
            TradeIds = trades.Select(t => t.id).ToList(),
            EntryPrice = trades.FirstOrDefault()?.LimitPrice ?? trades.FirstOrDefault()?.FilledPrice,
            Outcome = TriggerOutcome.Open
        };

        return await cosmo.CreateTriggerRecordAsync(record);
    }

    /// <summary>
    /// Updates trigger outcome when a trade fills. Computes P&L.
    /// </summary>
    public virtual async Task UpdateTriggerOutcomeAsync(string tradeId, decimal filledPrice, decimal filledQuantity)
    {
        var records = await cosmo.GetOpenTriggerRecordsByTradeIdAsync(tradeId);
        foreach (var record in records)
        {
            if (record.EntryPrice.HasValue && filledPrice > 0)
            {
                record.ExitPrice = filledPrice;
                record.PnL = (filledPrice - record.EntryPrice.Value) * filledQuantity;
                record.ReturnPercent = record.EntryPrice.Value != 0
                    ? ((filledPrice - record.EntryPrice.Value) / record.EntryPrice.Value) * 100
                    : 0;
                record.Outcome = record.PnL >= 0 ? TriggerOutcome.Win : TriggerOutcome.Loss;
            }
            else
            {
                record.ExitPrice = filledPrice;
                record.Outcome = TriggerOutcome.Win; // default if no entry price to compare
            }

            await cosmo.UpdateTriggerRecordAsync(record);
        }
    }

    /// <summary>
    /// Gets all trigger records for a strategy.
    /// </summary>
    public virtual async Task<List<StrategyTriggerRecord>> GetTriggerHistoryAsync(string accountId, string strategyId)
    {
        return await cosmo.GetTriggerRecordsAsync(accountId, strategyId);
    }

    /// <summary>
    /// Computes a performance summary from trigger records.
    /// </summary>
    public virtual async Task<StrategyPerformanceSummary> GetPerformanceSummaryAsync(string accountId, string strategyId)
    {
        var records = await cosmo.GetTriggerRecordsAsync(accountId, strategyId);

        var wins = records.Count(r => r.Outcome == TriggerOutcome.Win);
        var losses = records.Count(r => r.Outcome == TriggerOutcome.Loss);
        var open = records.Count(r => r.Outcome == TriggerOutcome.Open);
        var closed = wins + losses;

        var monthlyGroups = records
            .GroupBy(r => new { r.TriggeredUtc.Year, r.TriggeredUtc.Month })
            .Select(g => new MonthlyReturn
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                ReturnPercent = g.Where(r => r.ReturnPercent.HasValue).Sum(r => r.ReturnPercent!.Value),
                Triggers = g.Count()
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        return new StrategyPerformanceSummary
        {
            TotalTriggers = records.Count,
            Wins = wins,
            Losses = losses,
            OpenTrades = open,
            WinRate = closed > 0 ? (decimal)wins / closed * 100 : 0,
            TotalPnL = records.Where(r => r.PnL.HasValue).Sum(r => r.PnL!.Value),
            MonthlyReturns = monthlyGroups
        };
    }
}
