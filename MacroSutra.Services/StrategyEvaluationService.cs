using System.Collections.Concurrent;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Interfaces;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Background service that polls market data and evaluates active strategies
/// every 60 seconds during US market hours (9:30-16:00 ET, Mon-Fri).
/// </summary>
public class StrategyEvaluationService(
    IServiceScopeFactory scopeFactory,
    MarketDataService marketDataService,
    IStrategyEventPublisher? eventPublisher,
    ILogger<StrategyEvaluationService> logger) : BackgroundService
{
    private static readonly TimeSpan EvaluationInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeZoneInfo EasternTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    /// <summary>
    /// In-memory crossover state tracking. Key: strategyId:conditionId:symbol → previous value.
    /// Lost on restart, which is acceptable per design.
    /// </summary>
    private readonly ConcurrentDictionary<string, decimal> _previousValues = new();

    /// <summary>
    /// Timestamp of the last completed evaluation cycle. Null if never run.
    /// </summary>
    public DateTime? LastEvaluationUtc { get; private set; }

    /// <summary>
    /// Whether the service is actively running evaluations.
    /// </summary>
    public bool IsRunning { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StrategyEvaluationService started");
        IsRunning = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (IsMarketOpen())
                {
                    await EvaluateAllAsync(stoppingToken);
                    LastEvaluationUtc = DateTime.UtcNow;
                }
                else
                {
                    logger.LogDebug("Market is closed — skipping evaluation cycle");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during strategy evaluation cycle");
            }

            await Task.Delay(EvaluationInterval, stoppingToken);
        }

        IsRunning = false;
        logger.LogInformation("StrategyEvaluationService stopped");
    }

    /// <summary>
    /// Evaluates all active strategies. Can also be called manually for a single evaluation.
    /// </summary>
    public virtual async Task EvaluateAllAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var cosmo = scope.ServiceProvider.GetRequiredService<MacroSutraCosmo>();
        var conditionEvaluator = scope.ServiceProvider.GetRequiredService<ConditionEvaluator>();
        var executionService = scope.ServiceProvider.GetRequiredService<TradeExecutionService>();
        var dispatchService = scope.ServiceProvider.GetRequiredService<SubscriptionDispatchService>();
        var performanceService = scope.ServiceProvider.GetService<StrategyPerformanceService>();

        // 1. Get all active strategies (cross-partition)
        var strategies = await cosmo.GetAllActiveStrategiesAsync();
        if (strategies.Count == 0) return;

        logger.LogDebug("Evaluating {Count} active strategies", strategies.Count);

        // 2. Collect unique symbols and intervals
        var symbols = strategies.SelectMany(s => s.Symbols).Distinct().ToList();
        var intervals = strategies
            .SelectMany(s => s.TriggerGroups.Select(tg => tg.Interval))
            .Distinct().ToList();

        // 3. Fetch market data for all symbols and intervals
        var snapshots = new Dictionary<string, MarketSnapshot>();
        var priceHistories = new Dictionary<string, decimal[]>();

        foreach (var symbol in symbols)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var snapshot = await marketDataService.GetSnapshotAsync(symbol);
            if (snapshot != null)
                snapshots[symbol] = snapshot;

            foreach (var interval in intervals)
            {
                var key = $"{symbol}:{interval}";
                var history = await marketDataService.GetHistoricalPricesAsync(symbol, interval);
                if (history.Length > 0)
                    priceHistories[key] = history;
            }
        }

        // 4. Evaluate each strategy
        foreach (var strategy in strategies)
        {
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                await EvaluateStrategyAsync(strategy, snapshots, priceHistories, conditionEvaluator, executionService, dispatchService, cosmo, performanceService);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluating strategy {StrategyId}", strategy.id);
            }
        }
    }

    internal async Task EvaluateStrategyAsync(
        TradingStrategy strategy,
        Dictionary<string, MarketSnapshot> snapshots,
        Dictionary<string, decimal[]> priceHistories,
        ConditionEvaluator conditionEvaluator,
        TradeExecutionService executionService,
        SubscriptionDispatchService dispatchService,
        MacroSutraCosmo cosmo,
        StrategyPerformanceService? performanceService = null)
    {
        bool anyTriggered = false;

        foreach (var symbol in strategy.Symbols)
        {
            if (!snapshots.TryGetValue(symbol, out var snapshot)) continue;
            priceHistories.TryGetValue(symbol, out var history);
            history ??= [];

            foreach (var triggerGroup in strategy.TriggerGroups)
            {
                var intervalKey = $"{symbol}:{triggerGroup.Interval}";
                priceHistories.TryGetValue(intervalKey, out var groupHistory);
                groupHistory ??= history;
                // Append the live price so indicators reflect the latest trade
                if (snapshot.Price > 0)
                    groupHistory = [.. groupHistory, snapshot.Price];

                var strategyPrevValues = BuildPreviousValues(strategy.id, symbol, CollectConditions(triggerGroup.Conditions));
                var (groupTriggered, groupResults) = conditionEvaluator.EvaluateGroup(
                    triggerGroup.Conditions, snapshot, groupHistory, strategyPrevValues);
                WriteCrossoverState(strategy.id, symbol, groupResults);

                if (groupTriggered)
                {
                    logger.LogInformation("Strategy {StrategyName} ({StrategyId}) trigger group '{GroupName}' fired for {Symbol}",
                        strategy.Name, strategy.id, triggerGroup.Name, symbol);
                    var trades = await executionService.ExecuteActionsAsync(strategy, symbol, snapshot, triggerGroup.Actions);
                    anyTriggered = true;
                    await PostTriggerAsync(strategy, symbol, trades, dispatchService, performanceService);
                }
            }
        }

        // Update timestamps
        strategy.LastEvaluatedUtc = DateTime.UtcNow;
        if (anyTriggered)
            strategy.LastTriggeredUtc = DateTime.UtcNow;
        await cosmo.UpdateStrategyAsync(strategy);
    }

    private Dictionary<string, decimal> BuildPreviousValues(string strategyId, string symbol, List<TriggerCondition> conditions)
    {
        var prev = new Dictionary<string, decimal>();
        foreach (var condition in conditions)
        {
            var key = $"{strategyId}:{condition.ConditionId}:{symbol}";
            if (_previousValues.TryGetValue(key, out var val))
                prev[$"{condition.ConditionId}:{symbol}"] = val;
        }
        return prev;
    }

    private void WriteCrossoverState(string strategyId, string symbol, List<(bool passed, decimal value, string conditionId)> results)
    {
        foreach (var (_, value, conditionId) in results)
        {
            var key = $"{strategyId}:{conditionId}:{symbol}";
            _previousValues[key] = value;
        }
    }

    private async Task PostTriggerAsync(
        TradingStrategy strategy, string symbol, List<Trade> trades,
        SubscriptionDispatchService dispatchService, StrategyPerformanceService? performanceService)
    {
        if (eventPublisher != null && trades.Count > 0)
        {
            try
            {
                await eventPublisher.PublishStrategyTriggeredAsync(new StrategyAlertEvent
                {
                    StrategyId = strategy.id,
                    StrategyName = strategy.Name,
                    Symbol = symbol,
                    TriggeredUtc = DateTime.UtcNow,
                    TradeIds = trades.Select(t => t.id).ToList(),
                    TradeSide = trades.First().Side,
                    Quantity = trades.Sum(t => t.Quantity),
                    AccountId = strategy.AccountId
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish strategy alert for {StrategyId}", strategy.id);
            }
        }

        if (performanceService != null && trades.Count > 0)
        {
            try
            {
                await performanceService.RecordTriggerAsync(strategy.AccountId, strategy.id, symbol, trades);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to record performance trigger for {StrategyId}", strategy.id);
            }
        }

        try
        {
            await dispatchService.DispatchAsync(strategy, trades);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscription dispatch failed for strategy {StrategyId}", strategy.id);
        }
    }

    /// <summary>
    /// Evaluates a single strategy on demand (for the "Test Now" feature).
    /// Returns per-condition results without executing actions.
    /// </summary>
    public virtual async Task<StrategyEvaluationResult> EvaluateSingleAsync(TradingStrategy strategy)
    {
        var result = new StrategyEvaluationResult { EvaluatedUtc = DateTime.UtcNow };
        var tempPreviousValues = new Dictionary<string, decimal>();
        var perSymbolTriggered = new List<bool>();

        using var scope = scopeFactory.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<ConditionEvaluator>();

        foreach (var symbol in strategy.Symbols)
        {
            var snapshot = await marketDataService.GetSnapshotAsync(symbol);
            if (snapshot == null) continue;

            foreach (var triggerGroup in strategy.TriggerGroups)
            {
                var history = await marketDataService.GetHistoricalPricesAsync(symbol, triggerGroup.Interval);
                // Append the live price so indicators reflect the latest trade
                if (snapshot.Price > 0)
                    history = [.. history, snapshot.Price];
                var conditionMap = BuildConditionMap(triggerGroup.Conditions);
                var (groupTriggered, groupResults) = evaluator.EvaluateGroup(
                    triggerGroup.Conditions, snapshot, history, tempPreviousValues);
                perSymbolTriggered.Add(groupTriggered);

                foreach (var (passed, value, conditionId) in groupResults)
                {
                    conditionMap.TryGetValue(conditionId, out var cond);
                    result.Conditions.Add(new ConditionResult
                    {
                        ConditionId = conditionId,
                        ConditionType = cond?.ConditionType ?? ConditionType.Custom,
                        CurrentValue = value,
                        TargetValue = cond?.Value ?? 0,
                        Operator = cond?.Operator ?? ConditionOperator.Equal,
                        Passed = passed,
                        TriggerGroupName = triggerGroup.Name
                    });
                }
            }
        }

        result.WouldTrigger = perSymbolTriggered.Any(t => t);
        return result;
    }

    /// <summary>
    /// Recursively collects all conditions from a ConditionGroup tree.
    /// </summary>
    private static List<TriggerCondition> CollectConditions(ConditionGroup group)
    {
        var all = new List<TriggerCondition>(group.Conditions);
        foreach (var child in group.ChildGroups)
            all.AddRange(CollectConditions(child));
        return all;
    }

    /// <summary>
    /// Builds a lookup from conditionId to TriggerCondition for a group tree.
    /// </summary>
    private static Dictionary<string, TriggerCondition> BuildConditionMap(ConditionGroup group)
    {
        var map = new Dictionary<string, TriggerCondition>();
        foreach (var c in CollectConditions(group))
            map[c.ConditionId] = c;
        return map;
    }

    internal static bool IsMarketOpen()
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTime);

        // Monday = 1, Friday = 5
        if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
            return false;

        var marketOpen = new TimeOnly(9, 30);
        var marketClose = new TimeOnly(16, 0);
        var currentTime = TimeOnly.FromDateTime(now);

        return currentTime >= marketOpen && currentTime <= marketClose;
    }
}
