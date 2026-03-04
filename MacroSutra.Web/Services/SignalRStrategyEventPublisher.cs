using MacroSutra.Core.Interfaces;
using MacroSutra.Core.Models;
using MacroSutra.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MacroSutra.Web.Services;

/// <summary>
/// Publishes strategy and portfolio events via SignalR to account groups.
/// </summary>
public class SignalRStrategyEventPublisher(IHubContext<MacroSutraHub> hubContext) : IStrategyEventPublisher
{
    public async Task PublishStrategyTriggeredAsync(StrategyAlertEvent alert)
    {
        await hubContext.Clients.Group(alert.AccountId)
            .SendAsync("StrategyTriggered", alert);
    }

    public async Task PublishPortfolioUpdatedAsync(PortfolioUpdateEvent update)
    {
        await hubContext.Clients.Group(update.AccountId)
            .SendAsync("PortfolioUpdated", update);
    }
}
