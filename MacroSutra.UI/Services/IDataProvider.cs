using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.UI.Services;

/// <summary>
/// Result of syncing a brokerage account.
/// </summary>
public class SyncResultDto
{
    public int PositionCount { get; set; }
    public decimal? Balance { get; set; }
    public string? Error { get; set; }
}

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

    // Strategy templates
    Task<List<StrategyTemplate>> GetStrategyTemplatesAsync();
    Task<StrategyTemplate?> GetStrategyTemplateAsync(string id);

    // Trades
    Task<List<Trade>> GetTradesAsync(string accountId, string? symbol = null, TradeStatus? status = null, string? strategyId = null);
    Task<Trade?> GetTradeAsync(string id, string accountId);

    // Portfolio
    Task<List<BrokerageAccount>> GetBrokerageAccountsAsync(string accountId, bool activeOnly = false);
    Task<BrokerageAccount> CreateBrokerageAccountAsync(BrokerageAccount account);
    Task<BrokerageAccount> UpdateBrokerageAccountAsync(BrokerageAccount account);
    Task<BrokerageAccount> DeactivateBrokerageAccountAsync(string id, string accountId);
    Task<BrokerageAccount> ValidateAndLinkBrokerageAccountAsync(BrokerageAccount account);
    Task<List<Position>> GetPositionsAsync(string accountId, string? brokerageAccountId = null);

    // Sync
    Task<SyncResultDto> SyncBrokerageAccountAsync(string id, string accountId);
    Task<Dictionary<string, SyncResultDto>> SyncAllBrokerageAccountsAsync(string accountId);

    // Daisinet team import
    Task<List<DaisinetTeamMember>> GetDaisinetTeamMembersAsync();

    // Subscriptions
    Task<List<Subscription>> GetSubscriptionsAsync(string accountId);
    Task<Subscription?> GetSubscriptionAsync(string id, string accountId);
    Task<Subscription> SubscribeAsync(Subscription subscription);
    Task<Subscription> CancelSubscriptionAsync(string id, string accountId);
    Task<List<SubscriptionAction>> GetSubscriptionActionsAsync(string accountId, string? subscriptionId = null);
    Task<List<Subscription>> GetPublisherSubscriptionsAsync(string accountId);

    // Strategy evaluation
    Task<StrategyEvaluationResult> EvaluateStrategyAsync(string id, string accountId);

    // Backtesting
    Task<BacktestResult> RunBacktestAsync(string strategyId, string accountId, string userId, string symbol, DateOnly startDate, DateOnly endDate, decimal initialCapital, decimal slippageBps = 0, decimal commissionPerTrade = 0, string? timeFrame = null);

    // Walk-Forward Analysis
    Task<WalkForwardResult> RunWalkForwardAsync(string strategyId, string accountId, string userId, string symbol, DateOnly startDate, DateOnly endDate, decimal initialCapital, int inSampleDays = 252, int outOfSampleDays = 63, decimal slippageBps = 0, decimal commissionPerTrade = 0);
    Task<List<BacktestResult>> GetBacktestsAsync(string accountId, string? strategyId = null);
    Task<BacktestResult?> GetBacktestAsync(string id, string accountId);
    Task DeleteBacktestAsync(string id, string accountId);

    // Community
    Task<List<TradingStrategy>> GetPublicStrategiesAsync(int page = 0, int pageSize = 20, string? sortBy = null);
    Task<TradingStrategy?> GetPublicStrategyAsync(string strategyId);
    Task<StrategyCommunityStats?> GetCommunityStatsAsync(string strategyId);
    Task<List<StrategyReview>> GetReviewsAsync(string strategyId);
    Task<TradingStrategy> ForkStrategyAsync(string strategyId, string accountId, string userId);
    Task<StrategyReview> CreateReviewAsync(string strategyId, string accountId, string userId, string userName, int rating, string? text);
    Task DeleteReviewAsync(string reviewId, string strategyId, string accountId);
    Task<List<LeaderboardEntry>> GetLeaderboardAsync(string sortBy = "sharpe", int limit = 25);
}
