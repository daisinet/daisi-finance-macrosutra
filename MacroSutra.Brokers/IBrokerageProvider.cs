using MacroSutra.Core.Models;
using MacroSutra.Core.Models.Options;

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
    /// Whether this provider supports fractional share quantities.
    /// </summary>
    bool SupportsFractionalShares => false;

    /// <summary>
    /// Whether this provider supports options trading.
    /// </summary>
    bool SupportsOptions => false;

    /// <summary>
    /// Attempts to refresh credentials (e.g. OAuth token refresh).
    /// Returns updated credential JSON if refreshed, null if no refresh needed or not supported.
    /// </summary>
    Task<string?> TryRefreshCredentialsAsync(string credentialRef) => Task.FromResult<string?>(null);

    /// <summary>
    /// Retrieves the options chain for a given underlying symbol.
    /// </summary>
    Task<OptionsChain> GetOptionsChainAsync(string credentialRef, string underlyingSymbol, DateOnly? expiration = null) =>
        throw new NotSupportedException("This provider does not support options trading.");

    /// <summary>
    /// Places an options order.
    /// </summary>
    Task<string> PlaceOptionsOrderAsync(string credentialRef, Trade trade) =>
        throw new NotSupportedException("This provider does not support options trading.");
}
