using Daisi.SDK.Clients.V1.Orc;
using Daisi.Protos.V1;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;
using MacroSutra.UI.Services;

namespace MacroSutra.Web.Services;

/// <summary>
/// IDataProvider implementation for Blazor Server.
/// Delegates directly to the service layer (no HTTP round-trip).
/// </summary>
public class WebDataProvider(
    UserManagementService userService,
    StrategyService strategyService,
    TradeService tradeService,
    PortfolioService portfolioService,
    SubscriptionService subscriptionService,
    AccountClientFactory accountClientFactory) : IDataProvider
{
    // Users
    public async Task<MacroSutraUser> CreateUserAsync(MacroSutraUser user) =>
        await userService.CreateUserAsync(user);

    public async Task<MacroSutraUser?> GetUserByDaisinetIdAsync(string daisinetUserId, string accountId) =>
        await userService.GetUserByDaisinetIdAsync(daisinetUserId, accountId);

    public async Task<List<MacroSutraUser>> GetUsersAsync(string accountId, bool activeOnly = false) =>
        await userService.GetUsersAsync(accountId, activeOnly);

    public async Task<MacroSutraUser> UpdateUserAsync(MacroSutraUser user) =>
        await userService.UpdateUserAsync(user);

    public async Task<MacroSutraUser> DeactivateUserAsync(string id, string accountId) =>
        await userService.DeactivateUserAsync(id, accountId);

    public async Task<MacroSutraUser> ReactivateUserAsync(string id, string accountId) =>
        await userService.ReactivateUserAsync(id, accountId);

    // Strategies
    public async Task<TradingStrategy> CreateStrategyAsync(TradingStrategy strategy) =>
        await strategyService.CreateStrategyAsync(strategy);

    public async Task<TradingStrategy?> GetStrategyAsync(string id, string accountId) =>
        await strategyService.GetStrategyAsync(id, accountId);

    public async Task<List<TradingStrategy>> GetStrategiesAsync(string accountId, string? userId = null) =>
        await strategyService.GetStrategiesAsync(accountId, userId);

    public async Task<TradingStrategy> UpdateStrategyAsync(TradingStrategy strategy) =>
        await strategyService.UpdateStrategyAsync(strategy);

    public async Task<TradingStrategy> ActivateStrategyAsync(string id, string accountId) =>
        await strategyService.ActivateStrategyAsync(id, accountId);

    public async Task<TradingStrategy> DeactivateStrategyAsync(string id, string accountId) =>
        await strategyService.DeactivateStrategyAsync(id, accountId);

    public async Task DeleteStrategyAsync(string id, string accountId) =>
        await strategyService.DeleteStrategyAsync(id, accountId);

    // Trades
    public async Task<List<Trade>> GetTradesAsync(string accountId, string? symbol = null, TradeStatus? status = null, string? strategyId = null) =>
        await tradeService.GetTradesAsync(accountId, symbol, status, strategyId);

    public async Task<Trade?> GetTradeAsync(string id, string accountId) =>
        await tradeService.GetTradeAsync(id, accountId);

    // Portfolio
    public async Task<List<BrokerageAccount>> GetBrokerageAccountsAsync(string accountId, bool activeOnly = false) =>
        await portfolioService.GetBrokerageAccountsAsync(accountId, activeOnly);

    public async Task<BrokerageAccount> CreateBrokerageAccountAsync(BrokerageAccount account) =>
        await portfolioService.CreateBrokerageAccountAsync(account);

    public async Task<List<Position>> GetPositionsAsync(string accountId, string? brokerageAccountId = null) =>
        await portfolioService.GetPositionsAsync(accountId, brokerageAccountId);

    // Daisinet team import
    public async Task<List<DaisinetTeamMember>> GetDaisinetTeamMembersAsync()
    {
        var client = accountClientFactory.Create();
        var response = await client.GetUsersAsync(new GetUsersRequest
        {
            Paging = new PagingInfo { PageIndex = 0, PageSize = 100 }
        });

        return response.Users.Select(u => new DaisinetTeamMember
        {
            DaisinetUserId = u.Id,
            Name = u.Name,
            Email = u.EmailAddress,
            DaisinetRole = u.Role.ToString().Replace("UserRoles", "")
        }).ToList();
    }

    // Subscriptions
    public async Task<List<Subscription>> GetSubscriptionsAsync(string accountId) =>
        await subscriptionService.GetSubscriptionsBySubscriberAsync(accountId);

    public async Task<Subscription> CreateSubscriptionAsync(Subscription subscription) =>
        await subscriptionService.CreateSubscriptionAsync(subscription);

    public async Task<Subscription> CancelSubscriptionAsync(string id, string accountId) =>
        await subscriptionService.CancelSubscriptionAsync(id, accountId);
}
