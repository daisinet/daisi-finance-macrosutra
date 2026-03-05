using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Core.Models.Options;

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

    // Strategy performance
    Task<List<StrategyTriggerRecord>> GetTriggerHistoryAsync(string accountId, string strategyId);
    Task<StrategyPerformanceSummary> GetStrategyPerformanceAsync(string accountId, string strategyId);

    // Community
    Task<List<TradingStrategy>> GetPublicStrategiesAsync(int page = 0, int pageSize = 20, string? sortBy = null);
    Task<TradingStrategy?> GetPublicStrategyAsync(string strategyId);
    Task<StrategyCommunityStats?> GetCommunityStatsAsync(string strategyId);
    Task<List<StrategyReview>> GetReviewsAsync(string strategyId);
    Task<TradingStrategy> ForkStrategyAsync(string strategyId, string accountId, string userId);
    Task<StrategyReview> CreateReviewAsync(string strategyId, string accountId, string userId, string userName, int rating, string? text);
    Task DeleteReviewAsync(string reviewId, string strategyId, string accountId);
    Task<List<LeaderboardEntry>> GetLeaderboardAsync(string sortBy = "sharpe", int limit = 25);

    // Trade export
    Task<byte[]> ExportTradesCsvAsync(string accountId, string? symbol = null, string? strategyId = null);
    Task<byte[]> ExportTradesPdfAsync(string accountId, string? symbol = null, string? strategyId = null);

    // Historical market data (charting)
    Task<List<OhlcvBar>> GetHistoricalBarsAsync(string symbol, DateOnly from, DateOnly to, string timeFrame = "1D");

    // DCA schedules
    Task<List<DcaSchedule>> GetDcaSchedulesAsync(string accountId);
    Task<DcaSchedule?> GetDcaScheduleAsync(string id, string accountId);
    Task<DcaSchedule> CreateDcaScheduleAsync(DcaSchedule schedule);
    Task<DcaSchedule> UpdateDcaScheduleAsync(DcaSchedule schedule);
    Task DeleteDcaScheduleAsync(string id, string accountId);
    Task<DcaSchedule> ActivateDcaScheduleAsync(string id, string accountId);
    Task<DcaSchedule> DeactivateDcaScheduleAsync(string id, string accountId);

    // Portfolio rebalancing
    Task<List<RebalanceTarget>> GetRebalanceTargetsAsync(string accountId);
    Task<RebalanceTarget?> GetRebalanceTargetAsync(string id, string accountId);
    Task<RebalanceTarget> CreateRebalanceTargetAsync(RebalanceTarget target);
    Task<RebalanceTarget> UpdateRebalanceTargetAsync(RebalanceTarget target);
    Task DeleteRebalanceTargetAsync(string id, string accountId);
    Task<RebalanceAnalysis> AnalyzeRebalanceAsync(string targetId, string accountId);
    Task<List<Trade>> ExecuteRebalanceAsync(string targetId, string accountId);

    // Tax-loss harvesting
    Task<TaxLossHarvestingReport> GetTaxLossHarvestingReportAsync(string accountId, string? brokerageAccountId = null);

    // Options
    Task<OptionsChain> GetOptionsChainAsync(string accountId, string brokerageAccountId, string underlyingSymbol, DateOnly? expiration = null);
    Task<Trade> PlaceOptionsOrderAsync(Trade trade);
}
