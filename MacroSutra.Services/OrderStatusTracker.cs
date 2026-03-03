using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Background service that polls open trade orders every 30 seconds
/// and updates their status from the brokerage provider.
/// </summary>
public class OrderStatusTracker(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderStatusTracker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OrderStatusTracker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOpenOrdersAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking open orders");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        logger.LogInformation("OrderStatusTracker stopped");
    }

    internal virtual async Task CheckOpenOrdersAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var cosmo = scope.ServiceProvider.GetRequiredService<MacroSutraCosmo>();
        var portfolioService = scope.ServiceProvider.GetRequiredService<PortfolioService>();
        var providerFactory = scope.ServiceProvider.GetRequiredService<BrokerageProviderFactory>();
        var tradeService = scope.ServiceProvider.GetRequiredService<TradeService>();

        var openTrades = await cosmo.GetOpenTradesAsync();
        if (openTrades.Count == 0) return;

        logger.LogDebug("Checking {Count} open trades", openTrades.Count);

        foreach (var trade in openTrades)
        {
            try
            {
                // Skip trades without external order IDs (e.g., alerts, paper)
                if (string.IsNullOrEmpty(trade.ExternalOrderId) || string.IsNullOrEmpty(trade.BrokerageAccountId))
                    continue;

                var account = await portfolioService.GetBrokerageAccountAsync(trade.BrokerageAccountId, trade.AccountId);
                if (account == null || account.Provider == BrokerageProvider.Paper)
                    continue;

                var provider = providerFactory.GetProvider(account.Provider);
                var orderStatus = await provider.GetOrderStatusAsync(account.CredentialData, trade.ExternalOrderId);

                if (orderStatus.Status != trade.Status)
                {
                    logger.LogInformation("Trade {TradeId} status changed: {Old} → {New}",
                        trade.id, trade.Status, orderStatus.Status);

                    await tradeService.UpdateTradeStatusAsync(
                        trade.id, trade.AccountId, orderStatus.Status,
                        orderStatus.FilledPrice, orderStatus.FilledQuantity);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check status for trade {TradeId}", trade.id);
            }
        }
    }
}
