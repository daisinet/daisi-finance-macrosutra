using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.UI.Services;

/// <summary>
/// Unified data access abstraction.
/// Web implements via service layer directly; MAUI implements via SDK HTTP client.
/// </summary>
public interface IDataProvider
{
    // Users
    Task<MacroSutraUser> CreateUserAsync(MacroSutraUser user);
    Task<MacroSutraUser?> GetUserByDaisinetIdAsync(string daisinetUserId, string accountId);
    Task<List<MacroSutraUser>> GetUsersAsync(string accountId, bool activeOnly = false);
    Task<MacroSutraUser> UpdateUserAsync(MacroSutraUser user);
    Task<MacroSutraUser> DeactivateUserAsync(string id, string accountId);
    Task<MacroSutraUser> ReactivateUserAsync(string id, string accountId);

    // Strategies
    Task<TradingStrategy> CreateStrategyAsync(TradingStrategy strategy);
    Task<TradingStrategy?> GetStrategyAsync(string id, string accountId);
    Task<List<TradingStrategy>> GetStrategiesAsync(string accountId, string? userId = null);
    Task<TradingStrategy> UpdateStrategyAsync(TradingStrategy strategy);
    Task<TradingStrategy> ActivateStrategyAsync(string id, string accountId);
    Task<TradingStrategy> DeactivateStrategyAsync(string id, string accountId);
    Task DeleteStrategyAsync(string id, string accountId);

    // Trades
    Task<List<Trade>> GetTradesAsync(string accountId, string? symbol = null, TradeStatus? status = null, string? strategyId = null);
    Task<Trade?> GetTradeAsync(string id, string accountId);

    // Portfolio
    Task<List<BrokerageAccount>> GetBrokerageAccountsAsync(string accountId, bool activeOnly = false);
    Task<BrokerageAccount> CreateBrokerageAccountAsync(BrokerageAccount account);
    Task<List<Position>> GetPositionsAsync(string accountId, string? brokerageAccountId = null);

    // Daisinet team import
    Task<List<DaisinetTeamMember>> GetDaisinetTeamMembersAsync();

    // Subscriptions
    Task<List<Subscription>> GetSubscriptionsAsync(string accountId);
    Task<Subscription> CreateSubscriptionAsync(Subscription subscription);
    Task<Subscription> CancelSubscriptionAsync(string id, string accountId);
}
