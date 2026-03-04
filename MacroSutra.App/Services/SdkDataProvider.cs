using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.SDK;
using MacroSutra.UI.Services;

namespace MacroSutra.App.Services;

/// <summary>
/// IDataProvider that creates MacroSutraClient with the user's clientKey
/// from MauiAuthProvider, then delegates to SDK sub-clients.
/// </summary>
public class SdkDataProvider(MacroSutraClientFactory clientFactory, MauiAuthProvider authProvider) : IDataProvider
{
    private async Task<MacroSutraClient> GetClientAsync()
    {
        var clientKey = await authProvider.GetClientKeyAsync()
            ?? throw new InvalidOperationException("Not authenticated.");
        return clientFactory.Create(clientKey);
    }

    // Users — SDK endpoints not fully exposed for mutation in Phase 1
    public Task<MacroSutraUser> CreateUserAsync(MacroSutraUser user) =>
        throw new NotSupportedException("User creation is server-side only.");

    public Task<MacroSutraUser?> GetUserByDaisinetIdAsync(string daisinetUserId, string accountId) =>
        throw new NotSupportedException("Use GetCurrentUser via SDK.");

    public async Task<List<MacroSutraUser>> GetUsersAsync(string accountId, bool activeOnly = false)
    {
        using var client = await GetClientAsync();
        var sdkUsers = await client.Users.GetUsersAsync();
        return sdkUsers.Select(u => new MacroSutraUser
        {
            id = u.Id, AccountId = u.AccountId, Name = u.Name, Email = u.Email,
            DaisinetUserId = u.DaisinetUserId, IsActive = u.IsActive
        }).ToList();
    }

    public Task<MacroSutraUser> UpdateUserAsync(MacroSutraUser user) =>
        throw new NotSupportedException("User updates are server-side only.");

    public Task<MacroSutraUser> DeactivateUserAsync(string id, string accountId) =>
        throw new NotSupportedException("User management is server-side only.");

    public Task<MacroSutraUser> ReactivateUserAsync(string id, string accountId) =>
        throw new NotSupportedException("User management is server-side only.");

    // Strategy templates
    public async Task<List<StrategyTemplate>> GetStrategyTemplatesAsync()
    {
        using var client = await GetClientAsync();
        var sdkTemplates = await client.Strategies.GetTemplatesAsync();
        return sdkTemplates.Select(t => new StrategyTemplate
        {
            Id = t.Id, Name = t.Name, Description = t.Description, Category = t.Category,
            LogicGroup = Enum.TryParse<Core.Enums.LogicGroupType>(t.LogicGroup, true, out var lg) ? lg : Core.Enums.LogicGroupType.And,
            Conditions = t.Conditions.Select(c => new TriggerCondition
            {
                ConditionType = Enum.TryParse<ConditionType>(c.ConditionType, true, out var ct) ? ct : ConditionType.Price,
                Operator = Enum.TryParse<Core.Enums.ConditionOperator>(c.Operator, true, out var op) ? op : Core.Enums.ConditionOperator.GreaterThan,
                Value = c.Value, Period = c.Period
            }).ToList(),
            Actions = t.Actions.Select(a => new TradeAction
            {
                ActionType = Enum.TryParse<Core.Enums.TradeActionType>(a.ActionType, true, out var at) ? at : Core.Enums.TradeActionType.MarketOrder,
                Side = Enum.TryParse<Core.Enums.TradeSide>(a.Side, true, out var s) ? s : Core.Enums.TradeSide.Buy,
                QuantityType = Enum.TryParse<Core.Enums.QuantityType>(a.QuantityType, true, out var qt) ? qt : Core.Enums.QuantityType.Shares,
                Quantity = a.Quantity
            }).ToList()
        }).ToList();
    }

    public async Task<StrategyTemplate?> GetStrategyTemplateAsync(string id)
    {
        using var client = await GetClientAsync();
        var t = await client.Strategies.GetTemplateAsync(id);
        if (t == null) return null;
        return new StrategyTemplate
        {
            Id = t.Id, Name = t.Name, Description = t.Description, Category = t.Category,
            LogicGroup = Enum.TryParse<Core.Enums.LogicGroupType>(t.LogicGroup, true, out var lg) ? lg : Core.Enums.LogicGroupType.And,
            Conditions = t.Conditions.Select(c => new TriggerCondition
            {
                ConditionType = Enum.TryParse<ConditionType>(c.ConditionType, true, out var ct) ? ct : ConditionType.Price,
                Operator = Enum.TryParse<Core.Enums.ConditionOperator>(c.Operator, true, out var op) ? op : Core.Enums.ConditionOperator.GreaterThan,
                Value = c.Value, Period = c.Period
            }).ToList(),
            Actions = t.Actions.Select(a => new TradeAction
            {
                ActionType = Enum.TryParse<Core.Enums.TradeActionType>(a.ActionType, true, out var at) ? at : Core.Enums.TradeActionType.MarketOrder,
                Side = Enum.TryParse<Core.Enums.TradeSide>(a.Side, true, out var s) ? s : Core.Enums.TradeSide.Buy,
                QuantityType = Enum.TryParse<Core.Enums.QuantityType>(a.QuantityType, true, out var qt) ? qt : Core.Enums.QuantityType.Shares,
                Quantity = a.Quantity
            }).ToList()
        };
    }

    // Strategies
    public async Task<TradingStrategy> CreateStrategyAsync(TradingStrategy strategy)
    {
        using var client = await GetClientAsync();
        var sdkResult = await client.Strategies.CreateStrategyAsync(MapToSdkStrategy(strategy));
        strategy.id = sdkResult?.Id ?? "";
        return strategy;
    }

    public async Task<TradingStrategy?> GetStrategyAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Strategies.GetStrategyAsync(id);
        if (sdk == null) return null;
        return MapFromSdkStrategy(sdk);
    }

    public async Task<List<TradingStrategy>> GetStrategiesAsync(string accountId, string? userId = null)
    {
        using var client = await GetClientAsync();
        var sdkStrategies = await client.Strategies.GetStrategiesAsync();
        return sdkStrategies.Select(MapFromSdkStrategy).ToList();
    }

    public async Task<TradingStrategy> UpdateStrategyAsync(TradingStrategy strategy)
    {
        using var client = await GetClientAsync();
        var sdkModel = MapToSdkStrategy(strategy);
        sdkModel.Id = strategy.id;
        await client.Strategies.UpdateStrategyAsync(strategy.id, sdkModel);
        return strategy;
    }

    private static SDK.Models.TradingStrategy MapToSdkStrategy(TradingStrategy s) => new()
    {
        Id = s.id, Name = s.Name, Description = s.Description,
        Symbols = s.Symbols, IsActive = s.IsActive, IsPublic = s.IsPublic,
        LogicGroup = s.LogicGroup.ToString(),
        SizingMode = s.SizingMode.ToString(),
        Visibility = s.Visibility.ToString(),
        SubscriptionCreditPrice = s.SubscriptionCreditPrice,
        SubscriptionPeriodDays = s.SubscriptionPeriodDays,
        ForkedFromStrategyId = s.ForkedFromStrategyId,
        Conditions = s.Conditions.Select(c => new SDK.Models.StrategyCondition
        {
            ConditionId = c.ConditionId, ConditionType = c.ConditionType.ToString(),
            Operator = c.Operator.ToString(), Value = c.Value, Period = c.Period
        }).ToList(),
        Actions = s.Actions.Select(a => new SDK.Models.StrategyAction
        {
            ActionType = a.ActionType.ToString(), Side = a.Side.ToString(),
            QuantityType = a.QuantityType.ToString(), Quantity = a.Quantity
        }).ToList()
    };

    private static TradingStrategy MapFromSdkStrategy(SDK.Models.TradingStrategy sdk) => new()
    {
        id = sdk.Id, AccountId = sdk.AccountId, UserId = sdk.UserId,
        Name = sdk.Name, Description = sdk.Description,
        Symbols = sdk.Symbols, IsActive = sdk.IsActive, IsPublic = sdk.IsPublic,
        CreatedUtc = sdk.CreatedUtc,
        LastEvaluatedUtc = sdk.LastEvaluatedUtc,
        LastTriggeredUtc = sdk.LastTriggeredUtc,
        LogicGroup = Enum.TryParse<LogicGroupType>(sdk.LogicGroup, true, out var lg) ? lg : LogicGroupType.And,
        SizingMode = Enum.TryParse<SizingMode>(sdk.SizingMode, true, out var sm) ? sm : Core.Enums.SizingMode.Fixed,
        Visibility = Enum.TryParse<StrategyVisibility>(sdk.Visibility, true, out var v) ? v : StrategyVisibility.Private,
        SubscriptionCreditPrice = sdk.SubscriptionCreditPrice,
        SubscriptionPeriodDays = sdk.SubscriptionPeriodDays,
        ForkedFromStrategyId = sdk.ForkedFromStrategyId,
        Conditions = sdk.Conditions.Select(c => new TriggerCondition
        {
            ConditionId = c.ConditionId,
            ConditionType = Enum.TryParse<ConditionType>(c.ConditionType, true, out var ct) ? ct : ConditionType.Price,
            Operator = Enum.TryParse<ConditionOperator>(c.Operator, true, out var op) ? op : ConditionOperator.GreaterThan,
            Value = c.Value, Period = c.Period
        }).ToList(),
        Actions = sdk.Actions.Select(a => new TradeAction
        {
            ActionType = Enum.TryParse<TradeActionType>(a.ActionType, true, out var at) ? at : TradeActionType.MarketOrder,
            Side = Enum.TryParse<TradeSide>(a.Side, true, out var s) ? s : TradeSide.Buy,
            QuantityType = Enum.TryParse<QuantityType>(a.QuantityType, true, out var qt) ? qt : QuantityType.Shares,
            Quantity = a.Quantity
        }).ToList()
    };

    public async Task<TradingStrategy> ActivateStrategyAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Strategies.ActivateStrategyAsync(id);
        return new TradingStrategy
        {
            id = sdk?.Id ?? id, AccountId = sdk?.AccountId ?? accountId,
            Name = sdk?.Name ?? "", IsActive = true
        };
    }

    public async Task<TradingStrategy> DeactivateStrategyAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Strategies.DeactivateStrategyAsync(id);
        return new TradingStrategy
        {
            id = sdk?.Id ?? id, AccountId = sdk?.AccountId ?? accountId,
            Name = sdk?.Name ?? "", IsActive = false
        };
    }

    public async Task DeleteStrategyAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        await client.Strategies.DeleteStrategyAsync(id);
    }

    // Trades
    public async Task<List<Trade>> GetTradesAsync(string accountId, string? symbol = null, TradeStatus? status = null, string? strategyId = null)
    {
        using var client = await GetClientAsync();
        var sdkTrades = await client.Trades.GetTradesAsync(symbol, status?.ToString());
        return sdkTrades.Select(t => new Trade
        {
            id = t.Id, AccountId = t.AccountId, Symbol = t.Symbol,
            Quantity = t.Quantity, FilledPrice = t.FilledPrice,
            CreatedUtc = t.CreatedUtc, FilledUtc = t.FilledUtc
        }).ToList();
    }

    public async Task<Trade?> GetTradeAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var t = await client.Trades.GetTradeAsync(id);
        if (t == null) return null;
        return new Trade
        {
            id = t.Id, AccountId = t.AccountId, Symbol = t.Symbol,
            Quantity = t.Quantity, FilledPrice = t.FilledPrice,
            CreatedUtc = t.CreatedUtc, FilledUtc = t.FilledUtc
        };
    }

    // Portfolio
    public async Task<List<BrokerageAccount>> GetBrokerageAccountsAsync(string accountId, bool activeOnly = false)
    {
        using var client = await GetClientAsync();
        var sdkAccounts = await client.Portfolio.GetAccountsAsync();
        return sdkAccounts.Select(a => new BrokerageAccount
        {
            id = a.Id, AccountId = a.AccountId, Name = a.Name, IsActive = a.IsActive
        }).ToList();
    }

    public async Task<BrokerageAccount> CreateBrokerageAccountAsync(BrokerageAccount account)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Portfolio.CreateAccountAsync(new SDK.Models.BrokerageAccount
        {
            Name = account.Name, Provider = account.Provider.ToString(),
            IsPaperTrading = account.IsPaperTrading, CredentialData = account.CredentialData
        });
        return new BrokerageAccount
        {
            id = sdk?.Id ?? "", AccountId = sdk?.AccountId ?? account.AccountId,
            Name = sdk?.Name ?? account.Name, IsActive = sdk?.IsActive ?? true
        };
    }

    public async Task<BrokerageAccount> UpdateBrokerageAccountAsync(BrokerageAccount account)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Portfolio.UpdateAccountAsync(account.id, new SDK.Models.BrokerageAccount
        {
            Id = account.id, Name = account.Name, Provider = account.Provider.ToString(),
            IsPaperTrading = account.IsPaperTrading, CredentialData = account.CredentialData
        });
        return new BrokerageAccount
        {
            id = sdk?.Id ?? account.id, AccountId = sdk?.AccountId ?? account.AccountId,
            Name = sdk?.Name ?? account.Name, IsActive = sdk?.IsActive ?? true
        };
    }

    public async Task<BrokerageAccount> DeactivateBrokerageAccountAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        await client.Portfolio.DeactivateAccountAsync(id);
        return new BrokerageAccount { id = id, AccountId = accountId, IsActive = false };
    }

    public async Task<BrokerageAccount> ValidateAndLinkBrokerageAccountAsync(BrokerageAccount account)
    {
        // Delegates to the same POST /api/portfolio/accounts endpoint (API does validation)
        return await CreateBrokerageAccountAsync(account);
    }

    public async Task<SyncResultDto> SyncBrokerageAccountAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var result = await client.Portfolio.SyncAccountAsync(id);
        return new SyncResultDto { PositionCount = result.PositionCount, Balance = result.Balance, Error = result.Error };
    }

    public async Task<Dictionary<string, SyncResultDto>> SyncAllBrokerageAccountsAsync(string accountId)
    {
        using var client = await GetClientAsync();
        var results = await client.Portfolio.SyncAllAsync();
        return results.ToDictionary(
            kvp => kvp.Key,
            kvp => new SyncResultDto { PositionCount = kvp.Value.PositionCount, Balance = kvp.Value.Balance, Error = kvp.Value.Error });
    }

    public async Task<List<Position>> GetPositionsAsync(string accountId, string? brokerageAccountId = null)
    {
        using var client = await GetClientAsync();
        var sdkPositions = await client.Portfolio.GetPositionsAsync(brokerageAccountId);
        return sdkPositions.Select(p => new Position
        {
            id = p.Id, BrokerageAccountId = p.BrokerageAccountId,
            Symbol = p.Symbol, Quantity = p.Quantity,
            AverageCost = p.AverageCost, CurrentPrice = p.CurrentPrice
        }).ToList();
    }

    // Daisinet team import — not available in MAUI
    public Task<List<DaisinetTeamMember>> GetDaisinetTeamMembersAsync() =>
        throw new NotSupportedException("Team import is only available in the web app.");

    // Subscriptions
    public async Task<List<Subscription>> GetSubscriptionsAsync(string accountId)
    {
        using var client = await GetClientAsync();
        var sdkSubs = await client.Subscriptions.GetSubscriptionsAsync();
        return sdkSubs.Select(MapSubscription).ToList();
    }

    public async Task<Subscription?> GetSubscriptionAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Subscriptions.GetSubscriptionAsync(id);
        return sdk != null ? MapSubscription(sdk) : null;
    }

    public async Task<Subscription> SubscribeAsync(Subscription subscription)
    {
        using var client = await GetClientAsync();
        var request = new SDK.Models.CreateSubscriptionRequest
        {
            StrategyId = subscription.StrategyId,
            ActionType = subscription.ActionType.ToString(),
            ScaleFactor = subscription.ScaleFactor,
            BrokerageAccountId = subscription.BrokerageAccountId,
            WebhookUrl = subscription.WebhookUrl,
            NotificationEmail = subscription.NotificationEmail
        };
        var sdk = await client.Subscriptions.SubscribeAsync(request);
        return MapSubscription(sdk);
    }

    public async Task<Subscription> CancelSubscriptionAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        await client.Subscriptions.CancelAsync(id);
        return new Subscription { id = id, AccountId = accountId, IsActive = false };
    }

    public async Task<List<SubscriptionAction>> GetSubscriptionActionsAsync(string accountId, string? subscriptionId = null)
    {
        using var client = await GetClientAsync();
        var sdkActions = await client.Subscriptions.GetActionsAsync(subscriptionId ?? "");
        return sdkActions.Select(a => new SubscriptionAction
        {
            id = a.Id, AccountId = a.AccountId, SubscriptionId = a.SubscriptionId,
            StrategyId = a.StrategyId, TradeId = a.TradeId,
            Symbol = a.Symbol, Success = a.Success,
            ErrorMessage = a.ErrorMessage, ExecutedUtc = a.ExecutedUtc
        }).ToList();
    }

    public async Task<List<Subscription>> GetPublisherSubscriptionsAsync(string accountId)
    {
        using var client = await GetClientAsync();
        var sdkSubs = await client.Subscriptions.GetPublisherSubscriptionsAsync();
        return sdkSubs.Select(MapSubscription).ToList();
    }

    private static Subscription MapSubscription(SDK.Models.Subscription sdk)
    {
        return new Subscription
        {
            id = sdk.Id, AccountId = sdk.AccountId,
            SubscriberUserId = sdk.SubscriberUserId,
            PublisherAccountId = sdk.PublisherAccountId,
            StrategyId = sdk.StrategyId, StrategyName = sdk.StrategyName,
            PublisherName = sdk.PublisherName,
            ActionType = Enum.TryParse<Core.Enums.SubscriptionActionType>(sdk.ActionType, true, out var at)
                ? at : Core.Enums.SubscriptionActionType.Mirror,
            ScaleFactor = sdk.ScaleFactor, BrokerageAccountId = sdk.BrokerageAccountId,
            CreditPrice = sdk.CreditPrice, WebhookUrl = sdk.WebhookUrl,
            NotificationEmail = sdk.NotificationEmail,
            IsActive = sdk.IsActive, CreatedUtc = sdk.CreatedUtc
        };
    }

    // Backtesting
    public async Task<BacktestResult> RunBacktestAsync(string strategyId, string accountId, string userId, string symbol, DateOnly startDate, DateOnly endDate, decimal initialCapital, decimal slippageBps = 0, decimal commissionPerTrade = 0, string? timeFrame = null)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Backtests.RunBacktestAsync(new SDK.Models.BacktestRequest
        {
            StrategyId = strategyId,
            Symbol = symbol,
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            InitialCapital = initialCapital,
            SlippageBps = slippageBps,
            CommissionPerTrade = commissionPerTrade,
            TimeFrame = timeFrame
        });
        return MapBacktestResult(sdk);
    }

    // Walk-Forward Analysis
    public async Task<WalkForwardResult> RunWalkForwardAsync(string strategyId, string accountId, string userId, string symbol, DateOnly startDate, DateOnly endDate, decimal initialCapital, int inSampleDays = 252, int outOfSampleDays = 63, decimal slippageBps = 0, decimal commissionPerTrade = 0)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Backtests.RunWalkForwardAsync(new SDK.Models.WalkForwardRequest
        {
            StrategyId = strategyId,
            Symbol = symbol,
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            InitialCapital = initialCapital,
            InSampleDays = inSampleDays,
            OutOfSampleDays = outOfSampleDays,
            SlippageBps = slippageBps,
            CommissionPerTrade = commissionPerTrade
        });
        return new WalkForwardResult
        {
            Windows = sdk.Windows.Select(w => new WalkForwardWindow
            {
                StartDate = DateOnly.TryParse(w.StartDate, out var sd) ? sd : default,
                EndDate = DateOnly.TryParse(w.EndDate, out var ed) ? ed : default,
                IsInSample = w.IsInSample,
                TotalReturnPercent = w.TotalReturnPercent,
                SharpeRatio = w.SharpeRatio,
                MaxDrawdownPercent = w.MaxDrawdownPercent,
                TotalTrades = w.TotalTrades
            }).ToList(),
            Summary = sdk.Summary != null ? new WalkForwardSummary
            {
                AverageOosSharpe = sdk.Summary.AverageOosSharpe,
                AverageOosReturn = sdk.Summary.AverageOosReturn,
                ConsistencyScore = sdk.Summary.ConsistencyScore,
                TotalWindows = sdk.Summary.TotalWindows,
                ProfitableOosWindows = sdk.Summary.ProfitableOosWindows
            } : null
        };
    }

    public async Task<List<BacktestResult>> GetBacktestsAsync(string accountId, string? strategyId = null)
    {
        using var client = await GetClientAsync();
        var sdkResults = await client.Backtests.GetBacktestsAsync(strategyId);
        return sdkResults.Select(MapBacktestResult).ToList();
    }

    public async Task<BacktestResult?> GetBacktestAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Backtests.GetBacktestAsync(id);
        return sdk != null ? MapBacktestResult(sdk) : null;
    }

    public async Task DeleteBacktestAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        await client.Backtests.DeleteBacktestAsync(id);
    }

    private static BacktestResult MapBacktestResult(SDK.Models.BacktestResult sdk)
    {
        return new BacktestResult
        {
            id = sdk.Id,
            AccountId = sdk.AccountId,
            UserId = sdk.UserId,
            StrategyId = sdk.StrategyId,
            StrategyName = sdk.StrategyName,
            Symbol = sdk.Symbol,
            StartDate = DateOnly.TryParse(sdk.StartDate, out var sd) ? sd : default,
            EndDate = DateOnly.TryParse(sdk.EndDate, out var ed) ? ed : default,
            InitialCapital = sdk.InitialCapital,
            SlippageBps = sdk.SlippageBps,
            CommissionPerTrade = sdk.CommissionPerTrade,
            Status = Enum.TryParse<Core.Enums.BacktestStatus>(sdk.Status, true, out var st) ? st : Core.Enums.BacktestStatus.Pending,
            CreatedUtc = sdk.CreatedUtc,
            CompletedUtc = sdk.CompletedUtc,
            ErrorMessage = sdk.ErrorMessage,
            Metrics = sdk.Metrics != null ? new BacktestMetrics
            {
                TotalReturnPercent = sdk.Metrics.TotalReturnPercent,
                FinalEquity = sdk.Metrics.FinalEquity,
                MaxDrawdownPercent = sdk.Metrics.MaxDrawdownPercent,
                SharpeRatio = sdk.Metrics.SharpeRatio,
                WinRate = sdk.Metrics.WinRate,
                ProfitFactor = sdk.Metrics.ProfitFactor,
                TotalTrades = sdk.Metrics.TotalTrades,
                WinningTrades = sdk.Metrics.WinningTrades,
                LosingTrades = sdk.Metrics.LosingTrades
            } : null,
            EquityCurve = sdk.EquityCurve.Select(p => new EquityCurvePoint
            {
                Date = DateOnly.TryParse(p.Date, out var d) ? d : default,
                Equity = p.Equity,
                Drawdown = p.Drawdown
            }).ToList()
        };
    }

    // Strategy evaluation
    public async Task<StrategyEvaluationResult> EvaluateStrategyAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var sdkResult = await client.Strategies.EvaluateStrategyAsync(id);
        return new StrategyEvaluationResult
        {
            WouldTrigger = sdkResult.WouldTrigger,
            EvaluatedUtc = sdkResult.EvaluatedUtc,
            Conditions = sdkResult.Conditions.Select(c => new ConditionResult
            {
                ConditionId = c.ConditionId,
                ConditionType = Enum.TryParse<ConditionType>(c.ConditionType, true, out var ct) ? ct : ConditionType.Price,
                CurrentValue = c.CurrentValue,
                TargetValue = c.TargetValue,
                Operator = Enum.TryParse<ConditionOperator>(c.Operator, true, out var op) ? op : ConditionOperator.GreaterThan,
                Passed = c.Passed
            }).ToList()
        };
    }

    // Community
    public async Task<List<TradingStrategy>> GetPublicStrategiesAsync(int page = 0, int pageSize = 20, string? sortBy = null)
    {
        using var client = await GetClientAsync();
        var sdkStrategies = await client.Community.GetPublicStrategiesAsync(page, pageSize, sortBy);
        return sdkStrategies.Select(s => new TradingStrategy
        {
            id = s.Id, AccountId = s.AccountId, UserId = s.UserId,
            Name = s.Name, Description = s.Description,
            Symbols = s.Symbols, IsActive = s.IsActive, IsPublic = s.IsPublic,
            CreatedUtc = s.CreatedUtc
        }).ToList();
    }

    public async Task<TradingStrategy?> GetPublicStrategyAsync(string strategyId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Community.GetPublicStrategyAsync(strategyId);
        if (sdk == null) return null;
        return new TradingStrategy
        {
            id = sdk.Id, AccountId = sdk.AccountId, UserId = sdk.UserId,
            Name = sdk.Name, Description = sdk.Description,
            Symbols = sdk.Symbols, IsActive = sdk.IsActive, IsPublic = sdk.IsPublic,
            CreatedUtc = sdk.CreatedUtc
        };
    }

    public async Task<StrategyCommunityStats?> GetCommunityStatsAsync(string strategyId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Community.GetCommunityStatsAsync(strategyId);
        if (sdk == null) return null;
        return new StrategyCommunityStats
        {
            id = sdk.StrategyId, StrategyId = sdk.StrategyId,
            AverageRating = sdk.AverageRating, ReviewCount = sdk.ReviewCount,
            SubscriberCount = sdk.SubscriberCount, ForkCount = sdk.ForkCount
        };
    }

    public async Task<List<StrategyReview>> GetReviewsAsync(string strategyId)
    {
        using var client = await GetClientAsync();
        var sdkReviews = await client.Community.GetReviewsAsync(strategyId);
        return sdkReviews.Select(r => new StrategyReview
        {
            id = r.Id, StrategyId = r.StrategyId,
            ReviewerAccountId = r.ReviewerAccountId, ReviewerName = r.ReviewerName,
            Rating = r.Rating, ReviewText = r.ReviewText, CreatedUtc = r.CreatedUtc
        }).ToList();
    }

    public async Task<TradingStrategy> ForkStrategyAsync(string strategyId, string accountId, string userId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Community.ForkStrategyAsync(strategyId);
        return new TradingStrategy
        {
            id = sdk?.Id ?? "", AccountId = sdk?.AccountId ?? accountId,
            Name = sdk?.Name ?? "", Description = sdk?.Description ?? "",
            Symbols = sdk?.Symbols ?? new(), IsPublic = sdk?.IsPublic ?? false,
            CreatedUtc = sdk?.CreatedUtc ?? DateTime.UtcNow
        };
    }

    public async Task<StrategyReview> CreateReviewAsync(string strategyId, string accountId, string userId, string userName, int rating, string? text)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Community.CreateReviewAsync(strategyId, new SDK.Models.CreateReviewRequest
        {
            Rating = rating, Text = text
        });
        return new StrategyReview
        {
            id = sdk.Id, StrategyId = sdk.StrategyId,
            ReviewerAccountId = sdk.ReviewerAccountId, ReviewerName = sdk.ReviewerName,
            Rating = sdk.Rating, ReviewText = sdk.ReviewText, CreatedUtc = sdk.CreatedUtc
        };
    }

    public async Task DeleteReviewAsync(string reviewId, string strategyId, string accountId)
    {
        using var client = await GetClientAsync();
        await client.Community.DeleteReviewAsync(strategyId, reviewId);
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(string sortBy = "sharpe", int limit = 25)
    {
        using var client = await GetClientAsync();
        var sdkEntries = await client.Community.GetLeaderboardAsync(sortBy, limit);
        return sdkEntries.Select(e => new LeaderboardEntry
        {
            StrategyId = e.StrategyId, StrategyName = e.StrategyName,
            AuthorName = e.AuthorName, AccountId = e.AccountId,
            Symbols = e.Symbols, TotalReturnPercent = e.TotalReturnPercent,
            SharpeRatio = e.SharpeRatio, MaxDrawdownPercent = e.MaxDrawdownPercent,
            WinRate = e.WinRate, TotalBacktests = e.TotalBacktests,
            AverageRating = e.AverageRating, ReviewCount = e.ReviewCount,
            SubscriberCount = e.SubscriberCount
        }).ToList();
    }
}
