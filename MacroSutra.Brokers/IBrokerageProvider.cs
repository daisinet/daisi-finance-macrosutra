using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Interface for brokerage integrations. Each provider implements
/// this to connect to a specific brokerage for trade execution.
/// </summary>
public interface IBrokerageProvider
{
    Task<bool> ValidateCredentialsAsync(string credentialRef);
    Task<List<Position>> GetPositionsAsync(string credentialRef);
    Task<string> PlaceOrderAsync(string credentialRef, Trade trade);
    Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId);
    Task<decimal> GetAccountBalanceAsync(string credentialRef);

    /// <summary>
    /// Attempts to refresh credentials (e.g. OAuth token refresh).
    /// Returns updated credential JSON if refreshed, null if no refresh needed or not supported.
    /// </summary>
    Task<string?> TryRefreshCredentialsAsync(string credentialRef) => Task.FromResult<string?>(null);
}
