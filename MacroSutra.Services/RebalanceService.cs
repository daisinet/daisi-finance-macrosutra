using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Analyzes portfolio drift against target allocations and executes rebalancing trades.
/// </summary>
public class RebalanceService(
    MacroSutraCosmo cosmo,
    PortfolioService portfolioService,
    TradeService tradeService,
    BrokerageProviderFactory providerFactory,
    ILogger<RebalanceService> logger)
{
    public Task<RebalanceTarget> CreateTargetAsync(RebalanceTarget target) =>
        cosmo.CreateRebalanceTargetAsync(target);

    public Task<RebalanceTarget?> GetTargetAsync(string id, string accountId) =>
        cosmo.GetRebalanceTargetAsync(id, accountId);

    public Task<List<RebalanceTarget>> GetTargetsAsync(string accountId) =>
        cosmo.GetRebalanceTargetsAsync(accountId);

    public Task<RebalanceTarget> UpdateTargetAsync(RebalanceTarget target) =>
        cosmo.UpdateRebalanceTargetAsync(target);

    public Task DeleteTargetAsync(string id, string accountId) =>
        cosmo.DeleteRebalanceTargetAsync(id, accountId);

    /// <summary>
    /// Analyzes current positions against target allocations and returns drift data.
    /// </summary>
    public async Task<RebalanceAnalysis> AnalyzeAsync(string targetId, string accountId)
    {
        var target = await cosmo.GetRebalanceTargetAsync(targetId, accountId)
            ?? throw new InvalidOperationException("Rebalance target not found.");

        var positions = await portfolioService.GetPositionsAsync(accountId, target.BrokerageAccountId);

        var totalValue = positions
            .Where(p => p.MarketValue.HasValue)
            .Sum(p => p.MarketValue!.Value);

        var analysis = new RebalanceAnalysis
        {
            TotalPortfolioValue = totalValue,
            NeedsRebalancing = false
        };

        if (totalValue <= 0)
            return analysis;

        foreach (var alloc in target.Allocations)
        {
            var position = positions.FirstOrDefault(p =>
                string.Equals(p.Symbol, alloc.Symbol, StringComparison.OrdinalIgnoreCase));
            var actualValue = position?.MarketValue ?? 0;
            var actualPercent = totalValue > 0 ? actualValue / totalValue * 100m : 0;
            var driftPercent = actualPercent - alloc.TargetPercent;
            var suggestedTradeValue = (alloc.TargetPercent - actualPercent) / 100m * totalValue;

            var drift = new AllocationDrift
            {
                Symbol = alloc.Symbol,
                ActualPercent = Math.Round(actualPercent, 2),
                TargetPercent = alloc.TargetPercent,
                DriftPercent = Math.Round(driftPercent, 2),
                SuggestedTradeValue = Math.Round(suggestedTradeValue, 2)
            };

            analysis.Drifts.Add(drift);

            if (Math.Abs(driftPercent) > target.DriftThresholdPercent)
                analysis.NeedsRebalancing = true;
        }

        return analysis;
    }

    /// <summary>
    /// Executes rebalancing by placing buy/sell orders based on analysis drift.
    /// </summary>
    public async Task<List<Trade>> ExecuteRebalanceAsync(string targetId, string accountId)
    {
        var analysis = await AnalyzeAsync(targetId, accountId);
        var target = await cosmo.GetRebalanceTargetAsync(targetId, accountId)!;
        if (target == null) throw new InvalidOperationException("Target not found.");

        var trades = new List<Trade>();

        var account = await portfolioService.GetBrokerageAccountAsync(target.BrokerageAccountId, accountId);
        if (account == null)
        {
            logger.LogWarning("Brokerage account {Id} not found for rebalance", target.BrokerageAccountId);
            return trades;
        }

        var provider = providerFactory.GetProvider(account.Provider);

        foreach (var drift in analysis.Drifts.Where(d => Math.Abs(d.DriftPercent) > target.DriftThresholdPercent))
        {
            var side = drift.SuggestedTradeValue > 0 ? TradeSide.Buy : TradeSide.Sell;
            var dollarAmount = Math.Abs(drift.SuggestedTradeValue);

            // Estimate quantity (simplified — actual price fetching happens at execution)
            var position = (await portfolioService.GetPositionsAsync(accountId, target.BrokerageAccountId))
                .FirstOrDefault(p => string.Equals(p.Symbol, drift.Symbol, StringComparison.OrdinalIgnoreCase));
            var price = position?.CurrentPrice ?? 0;
            if (price <= 0) continue;

            var quantity = Math.Floor(dollarAmount / price * 100m) / 100m;
            if (quantity <= 0) continue;

            var trade = new Trade
            {
                AccountId = accountId,
                UserId = "",
                BrokerageAccountId = target.BrokerageAccountId,
                Symbol = drift.Symbol,
                Side = side,
                OrderType = TradeActionType.MarketOrder,
                Quantity = quantity,
                Status = TradeStatus.Pending,
                Notes = $"Rebalance: {target.Name}"
            };

            trade = await tradeService.RecordTradeAsync(trade);

            try
            {
                var externalId = await provider.PlaceOrderAsync(account.CredentialData, trade);
                trade.ExternalOrderId = externalId;
                trade.Status = TradeStatus.Submitted;
                await tradeService.UpdateTradeStatusAsync(trade.id, trade.AccountId, TradeStatus.Submitted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Rebalance order failed for {Symbol}", drift.Symbol);
                trade.Status = TradeStatus.Failed;
                await tradeService.UpdateTradeStatusAsync(trade.id, trade.AccountId, TradeStatus.Failed);
            }

            trades.Add(trade);
        }

        return trades;
    }
}
