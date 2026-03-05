using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Background service that polls for due DCA schedules and places dollar-amount market orders.
/// Runs every 60 seconds during US market hours (9:30 AM - 4:00 PM ET).
/// </summary>
public class DcaExecutionService(
    MacroSutraCosmo cosmo,
    IServiceScopeFactory scopeFactory,
    MarketDataService marketDataService,
    ILogger<DcaExecutionService> logger) : BackgroundService
{
    private static readonly TimeZoneInfo EasternTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (IsMarketHours())
                    await ProcessDueSchedulesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing DCA schedules");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    internal async Task ProcessDueSchedulesAsync()
    {
        var dueSchedules = await cosmo.GetActiveDcaSchedulesDueAsync();

        foreach (var schedule in dueSchedules)
        {
            try
            {
                await ExecuteScheduleAsync(schedule);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute DCA schedule {ScheduleId} for {Symbol}", schedule.id, schedule.Symbol);
            }
        }
    }

    internal async Task ExecuteScheduleAsync(DcaSchedule schedule)
    {
        using var scope = scopeFactory.CreateScope();
        var portfolioService = scope.ServiceProvider.GetRequiredService<PortfolioService>();
        var tradeService = scope.ServiceProvider.GetRequiredService<TradeService>();
        var providerFactory = scope.ServiceProvider.GetRequiredService<BrokerageProviderFactory>();

        var account = await portfolioService.GetBrokerageAccountAsync(schedule.BrokerageAccountId, schedule.AccountId);
        if (account == null)
        {
            logger.LogWarning("DCA schedule {Id}: brokerage account {BrokerId} not found — skipping", schedule.id, schedule.BrokerageAccountId);
            return;
        }

        // Get current price to compute quantity
        var snapshot = await marketDataService.GetSnapshotAsync(schedule.Symbol);
        if (snapshot == null || snapshot.Price <= 0)
        {
            logger.LogWarning("DCA schedule {Id}: unable to get price for {Symbol} — skipping", schedule.id, schedule.Symbol);
            return;
        }

        var quantity = Math.Floor(schedule.InvestmentAmount / snapshot.Price * 100m) / 100m;
        if (quantity <= 0)
        {
            logger.LogWarning("DCA schedule {Id}: resolved quantity is 0 for {Symbol} at {Price}", schedule.id, schedule.Symbol, snapshot.Price);
            return;
        }

        // Place market buy order
        var trade = new Trade
        {
            AccountId = schedule.AccountId,
            UserId = schedule.UserId,
            BrokerageAccountId = schedule.BrokerageAccountId,
            Symbol = schedule.Symbol,
            Side = TradeSide.Buy,
            OrderType = TradeActionType.MarketOrder,
            Quantity = quantity,
            Status = TradeStatus.Pending,
            Notes = $"DCA: {schedule.Name}"
        };

        trade = await tradeService.RecordTradeAsync(trade);

        try
        {
            var provider = providerFactory.GetProvider(account.Provider);
            var externalId = await provider.PlaceOrderAsync(account.CredentialData, trade);
            trade.ExternalOrderId = externalId;
            trade.Status = TradeStatus.Submitted;
            await tradeService.UpdateTradeStatusAsync(trade.id, trade.AccountId, TradeStatus.Submitted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DCA order placement failed for schedule {Id}", schedule.id);
            trade.Status = TradeStatus.Failed;
            trade.Notes = $"DCA failed: {ex.Message}";
            await tradeService.UpdateTradeStatusAsync(trade.id, trade.AccountId, TradeStatus.Failed);
        }

        // Update schedule
        schedule.LastExecutedUtc = DateTime.UtcNow;
        schedule.TotalExecutions++;
        schedule.TotalInvested += schedule.InvestmentAmount;
        schedule.NextExecutionUtc = DcaService.ComputeNextExecutionUtc(schedule, DateTime.UtcNow);
        await cosmo.UpdateDcaScheduleAsync(schedule);
    }

    internal static bool IsMarketHours()
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTime);
        return now.DayOfWeek is >= System.DayOfWeek.Monday and <= System.DayOfWeek.Friday
            && now.TimeOfDay >= new TimeSpan(9, 30, 0)
            && now.TimeOfDay <= new TimeSpan(16, 0, 0);
    }
}
