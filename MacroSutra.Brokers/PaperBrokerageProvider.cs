using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Paper trading provider that returns mock data for development and testing.
/// </summary>
public class PaperBrokerageProvider : IBrokerageProvider
{
    public Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        return Task.FromResult(true);
    }

    public Task<List<Position>> GetPositionsAsync(string credentialRef)
    {
        return Task.FromResult(new List<Position>());
    }

    public Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        var orderId = $"PAPER-{Guid.NewGuid().ToString("N")[..8]}";
        return Task.FromResult(orderId);
    }

    public Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        return Task.FromResult(new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = TradeStatus.Filled,
            FilledUtc = DateTime.UtcNow
        });
    }

    public Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        return Task.FromResult(100_000m);
    }
}
