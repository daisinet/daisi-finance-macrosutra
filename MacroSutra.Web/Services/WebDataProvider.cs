using Daisi.SDK.Clients.V1.Orc;
using Daisi.Protos.V1;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;
using MacroSutra.UI.Services;
using Microsoft.Extensions.DependencyInjection;

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
    PositionSyncService syncService,
    SubscriptionService subscriptionService,
    AccountClientFactory accountClientFactory,
    BacktestService backtestService,
    CommunityService communityService,
    IServiceProvider serviceProvider) : IDataProvider
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

    // Strategy templates
    public Task<List<StrategyTemplate>> GetStrategyTemplatesAsync()
    {
        var templateService = serviceProvider.GetRequiredService<StrategyTemplateService>();
        return Task.FromResult(templateService.GetTemplates());
    }

    public Task<StrategyTemplate?> GetStrategyTemplateAsync(string id)
    {
        var templateService = serviceProvider.GetRequiredService<StrategyTemplateService>();
        return Task.FromResult(templateService.GetTemplate(id));
    }

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

    public async Task<BrokerageAccount> UpdateBrokerageAccountAsync(BrokerageAccount account) =>
        await portfolioService.UpdateBrokerageAccountAsync(account);

    public async Task<BrokerageAccount> DeactivateBrokerageAccountAsync(string id, string accountId) =>
        await portfolioService.DeactivateBrokerageAccountAsync(id, accountId);

    public async Task<BrokerageAccount> ValidateAndLinkBrokerageAccountAsync(BrokerageAccount account) =>
        await portfolioService.ValidateAndCreateBrokerageAccountAsync(account);

    public async Task<List<Position>> GetPositionsAsync(string accountId, string? brokerageAccountId = null) =>
        await portfolioService.GetPositionsAsync(accountId, brokerageAccountId);

    // Sync
    public async Task<SyncResultDto> SyncBrokerageAccountAsync(string id, string accountId)
    {
        var account = await portfolioService.GetBrokerageAccountAsync(id, accountId)
            ?? throw new InvalidOperationException("Brokerage account not found.");
        var result = await syncService.SyncAccountAsync(account);
        return new SyncResultDto { PositionCount = result.PositionCount, Balance = result.Balance, Error = result.Error };
    }

    public async Task<Dictionary<string, SyncResultDto>> SyncAllBrokerageAccountsAsync(string accountId)
    {
        var results = await syncService.SyncAllAccountsAsync(accountId);
        return results.ToDictionary(
            kvp => kvp.Key,
            kvp => new SyncResultDto { PositionCount = kvp.Value.PositionCount, Balance = kvp.Value.Balance, Error = kvp.Value.Error });
    }

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

    public async Task<Subscription?> GetSubscriptionAsync(string id, string accountId) =>
        await subscriptionService.GetSubscriptionAsync(id, accountId);

    public async Task<Subscription> SubscribeAsync(Subscription subscription)
    {
        // Bill credits via Daisinet marketplace if price > 0
        if (subscription.CreditPrice > 0)
        {
            var client = serviceProvider.GetRequiredService<MarketplaceClientFactory>().Create();
            var marketplaceItemId = $"macrosutra-sub:{subscription.StrategyId}";
            var response = await client.PurchaseMarketplaceItemAsync(
                new PurchaseMarketplaceItemRequest
                {
                    MarketplaceItemId = marketplaceItemId
                });
            subscription.MarketplacePurchaseId = response?.Purchase?.Id;
        }

        return await subscriptionService.SubscribeAsync(subscription);
    }

    public async Task<Subscription> CancelSubscriptionAsync(string id, string accountId) =>
        await subscriptionService.CancelSubscriptionAsync(id, accountId);

    public async Task<List<SubscriptionAction>> GetSubscriptionActionsAsync(string accountId, string? subscriptionId = null) =>
        await subscriptionService.GetSubscriptionActionsAsync(accountId, subscriptionId);

    public async Task<List<Subscription>> GetPublisherSubscriptionsAsync(string accountId) =>
        await subscriptionService.GetPublisherSubscriptionsAsync(accountId);

    // Strategy evaluation
    public async Task<StrategyEvaluationResult> EvaluateStrategyAsync(string id, string accountId)
    {
        var strategy = await strategyService.GetStrategyAsync(id, accountId)
            ?? throw new InvalidOperationException("Strategy not found.");
        var evalService = serviceProvider.GetRequiredService<StrategyEvaluationService>();
        return await evalService.EvaluateSingleAsync(strategy);
    }

    // Backtesting
    public async Task<BacktestResult> RunBacktestAsync(string strategyId, string accountId, string userId, string symbol, DateOnly startDate, DateOnly endDate, decimal initialCapital, decimal slippageBps = 0, decimal commissionPerTrade = 0, string? timeFrame = null) =>
        await backtestService.CreateAndRunBacktestAsync(strategyId, accountId, userId, symbol, startDate, endDate, initialCapital, slippageBps, commissionPerTrade, timeFrame);

    // Walk-Forward Analysis
    public async Task<WalkForwardResult> RunWalkForwardAsync(string strategyId, string accountId, string userId, string symbol, DateOnly startDate, DateOnly endDate, decimal initialCapital, int inSampleDays = 252, int outOfSampleDays = 63, decimal slippageBps = 0, decimal commissionPerTrade = 0)
    {
        var walkForwardService = serviceProvider.GetRequiredService<WalkForwardService>();
        return await walkForwardService.RunAsync(strategyId, accountId, userId, symbol, startDate, endDate, initialCapital, inSampleDays, outOfSampleDays, slippageBps, commissionPerTrade);
    }

    public async Task<List<BacktestResult>> GetBacktestsAsync(string accountId, string? strategyId = null) =>
        await backtestService.GetBacktestsAsync(accountId, strategyId);

    public async Task<BacktestResult?> GetBacktestAsync(string id, string accountId) =>
        await backtestService.GetBacktestAsync(id, accountId);

    public async Task DeleteBacktestAsync(string id, string accountId) =>
        await backtestService.DeleteBacktestAsync(id, accountId);

    // Strategy performance
    public async Task<List<StrategyTriggerRecord>> GetTriggerHistoryAsync(string accountId, string strategyId)
    {
        var performanceService = serviceProvider.GetRequiredService<StrategyPerformanceService>();
        return await performanceService.GetTriggerHistoryAsync(accountId, strategyId);
    }

    public async Task<StrategyPerformanceSummary> GetStrategyPerformanceAsync(string accountId, string strategyId)
    {
        var performanceService = serviceProvider.GetRequiredService<StrategyPerformanceService>();
        return await performanceService.GetPerformanceSummaryAsync(accountId, strategyId);
    }

    // Community
    public async Task<List<TradingStrategy>> GetPublicStrategiesAsync(int page = 0, int pageSize = 20, string? sortBy = null) =>
        await communityService.GetPublicStrategiesAsync(page, pageSize, sortBy);

    public async Task<TradingStrategy?> GetPublicStrategyAsync(string strategyId) =>
        await communityService.GetPublicStrategyAsync(strategyId);

    public async Task<StrategyCommunityStats?> GetCommunityStatsAsync(string strategyId) =>
        await communityService.GetCommunityStatsAsync(strategyId);

    public async Task<List<StrategyReview>> GetReviewsAsync(string strategyId) =>
        await communityService.GetReviewsAsync(strategyId);

    public async Task<TradingStrategy> ForkStrategyAsync(string strategyId, string accountId, string userId) =>
        await communityService.ForkStrategyAsync(strategyId, accountId, userId);

    public async Task<StrategyReview> CreateReviewAsync(string strategyId, string accountId, string userId, string userName, int rating, string? text) =>
        await communityService.CreateReviewAsync(strategyId, accountId, userId, userName, rating, text);

    public async Task DeleteReviewAsync(string reviewId, string strategyId, string accountId) =>
        await communityService.DeleteReviewAsync(reviewId, strategyId, accountId);

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(string sortBy = "sharpe", int limit = 25) =>
        await communityService.GetLeaderboardAsync(sortBy, limit);
}
