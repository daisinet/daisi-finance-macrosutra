using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// Trade recording, status updates, and filtered queries.
/// </summary>
public class TradeService(MacroSutraCosmo cosmo)
{
    public virtual async Task<Trade> RecordTradeAsync(Trade trade)
    {
        return await cosmo.CreateTradeAsync(trade);
    }

    public virtual async Task<Trade?> GetTradeAsync(string id, string accountId)
    {
        return await cosmo.GetTradeAsync(id, accountId);
    }

    public virtual async Task<List<Trade>> GetTradesAsync(string accountId, string? symbol = null, TradeStatus? status = null, string? strategyId = null)
    {
        return await cosmo.GetTradesAsync(accountId, symbol, status, strategyId);
    }

    public virtual async Task<Trade> UpdateTradeStatusAsync(string id, string accountId, TradeStatus newStatus, decimal? filledPrice = null, decimal? filledQuantity = null)
    {
        var trade = await cosmo.GetTradeAsync(id, accountId)
            ?? throw new InvalidOperationException("Trade not found.");

        trade.Status = newStatus;
        if (filledPrice.HasValue)
            trade.FilledPrice = filledPrice;
        if (filledQuantity.HasValue)
            trade.FilledQuantity = filledQuantity;
        if (newStatus == TradeStatus.Filled)
            trade.FilledUtc = DateTime.UtcNow;

        return await cosmo.UpdateTradeAsync(trade);
    }
}
