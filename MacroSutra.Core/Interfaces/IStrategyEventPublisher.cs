using MacroSutra.Core.Models;

namespace MacroSutra.Core.Interfaces;

/// <summary>
/// Publishes strategy and portfolio events to connected clients.
/// Lives in Core to keep Services decoupled from SignalR.
/// </summary>
public interface IStrategyEventPublisher
{
    Task PublishStrategyTriggeredAsync(StrategyAlertEvent alert);
    Task PublishPortfolioUpdatedAsync(PortfolioUpdateEvent update);
}
