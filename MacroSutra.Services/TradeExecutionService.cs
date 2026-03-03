using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Executes trade actions when a strategy triggers.
/// Resolves quantities, places orders via brokerage providers, and records trades.
/// </summary>
public class TradeExecutionService(
    BrokerageProviderFactory providerFactory,
    PortfolioService portfolioService,
    TradeService tradeService,
    ILogger<TradeExecutionService> logger)
{
    /// <summary>
    /// Executes all actions for a triggered strategy against a specific symbol.
    /// </summary>
    public virtual async Task<List<Trade>> ExecuteActionsAsync(
        TradingStrategy strategy, string symbol, MarketSnapshot snapshot)
    {
        var trades = new List<Trade>();

        foreach (var action in strategy.Actions)
        {
            try
            {
                var trade = await ExecuteActionAsync(strategy, action, symbol, snapshot);
                if (trade != null)
                    trades.Add(trade);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute action {ActionId} for strategy {StrategyId} on {Symbol}",
                    action.ActionId, strategy.id, symbol);
            }
        }

        return trades;
    }

    internal virtual async Task<Trade?> ExecuteActionAsync(
        TradingStrategy strategy, TradeAction action, string symbol, MarketSnapshot snapshot)
    {
        // Alert actions — record as filled trade with notes
        if (action.ActionType == TradeActionType.Alert)
        {
            var alertTrade = new Trade
            {
                AccountId = strategy.AccountId,
                UserId = strategy.UserId,
                StrategyId = strategy.id,
                BrokerageAccountId = strategy.BrokerageAccountId,
                Symbol = symbol,
                Side = action.Side,
                OrderType = TradeActionType.Alert,
                Quantity = 0,
                Status = TradeStatus.Filled,
                FilledUtc = DateTime.UtcNow,
                Notes = $"Alert: {strategy.Name} triggered for {symbol} at {snapshot.Price:C}"
            };
            return await tradeService.RecordTradeAsync(alertTrade);
        }

        // Need a brokerage account for actual orders
        if (string.IsNullOrEmpty(strategy.BrokerageAccountId))
        {
            logger.LogWarning("Strategy {StrategyId} has no brokerage account — skipping order for {Symbol}",
                strategy.id, symbol);
            return null;
        }

        var brokerageAccount = await portfolioService.GetBrokerageAccountAsync(
            strategy.BrokerageAccountId, strategy.AccountId);
        if (brokerageAccount == null)
        {
            logger.LogWarning("Brokerage account {BrokerageAccountId} not found for strategy {StrategyId}",
                strategy.BrokerageAccountId, strategy.id);
            return null;
        }

        // Resolve quantity
        var provider = providerFactory.GetProvider(brokerageAccount.Provider);
        var balance = brokerageAccount.CachedBalance ?? await provider.GetAccountBalanceAsync(brokerageAccount.CredentialData);
        var quantity = ResolveQuantity(action, snapshot.Price, balance);

        if (quantity <= 0)
        {
            logger.LogWarning("Resolved quantity is 0 for action {ActionId} on {Symbol}", action.ActionId, symbol);
            return null;
        }

        // Build and record the trade
        var trade = new Trade
        {
            AccountId = strategy.AccountId,
            UserId = strategy.UserId,
            StrategyId = strategy.id,
            BrokerageAccountId = strategy.BrokerageAccountId,
            Symbol = symbol,
            Side = action.Side,
            OrderType = action.ActionType,
            Quantity = quantity,
            LimitPrice = action.LimitPrice,
            StopPrice = action.StopPrice,
            Status = TradeStatus.Pending
        };

        trade = await tradeService.RecordTradeAsync(trade);

        // Place the order
        try
        {
            var externalOrderId = await provider.PlaceOrderAsync(brokerageAccount.CredentialData, trade);
            trade.ExternalOrderId = externalOrderId;
            trade.Status = TradeStatus.Submitted;
            await tradeService.UpdateTradeStatusAsync(trade.id, trade.AccountId, TradeStatus.Submitted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Order placement failed for trade {TradeId}", trade.id);
            trade.Status = TradeStatus.Failed;
            trade.Notes = $"Order failed: {ex.Message}";
            await tradeService.UpdateTradeStatusAsync(trade.id, trade.AccountId, TradeStatus.Failed);
        }

        return trade;
    }

    /// <summary>
    /// Resolves the number of shares to trade based on the action's quantity type.
    /// </summary>
    internal static decimal ResolveQuantity(TradeAction action, decimal currentPrice, decimal accountBalance)
    {
        if (currentPrice <= 0) return 0;

        return action.QuantityType switch
        {
            QuantityType.Shares => action.Quantity,
            QuantityType.DollarAmount => Math.Floor(action.Quantity / currentPrice * 100) / 100, // 2 decimal places
            QuantityType.PercentOfPortfolio => Math.Floor(accountBalance * action.Quantity / 100 / currentPrice * 100) / 100,
            _ => action.Quantity
        };
    }
}
