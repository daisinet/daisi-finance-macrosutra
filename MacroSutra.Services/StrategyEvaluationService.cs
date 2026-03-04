using System.Collections.Concurrent;
using MacroSutra.Core.Enums;
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

        // 1. Get all active strategies (cross-partition)
        var strategies = await cosmo.GetAllActiveStrategiesAsync();
        if (strategies.Count == 0) return;

        logger.LogDebug("Evaluating {Count} active strategies", strategies.Count);

        // 2. Collect unique symbols
        var symbols = strategies.SelectMany(s => s.Symbols).Distinct().ToList();

        // 3. Fetch market data for all symbols
        var snapshots = new Dictionary<string, MarketSnapshot>();
        var priceHistories = new Dictionary<string, decimal[]>();

        foreach (var symbol in symbols)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var snapshot = await marketDataService.GetSnapshotAsync(symbol);
            if (snapshot != null)
                snapshots[symbol] = snapshot;

            var history = await marketDataService.GetHistoricalPricesAsync(symbol);
            if (history.Length > 0)
                priceHistories[symbol] = history;
        }

        // 4. Evaluate each strategy
        foreach (var strategy in strategies)
        {
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                await EvaluateStrategyAsync(strategy, snapshots, priceHistories, conditionEvaluator, executionService, dispatchService, cosmo);
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
        MacroSutraCosmo cosmo)
    {
        bool anyTriggered = false;

        foreach (var symbol in strategy.Symbols)
        {
            if (!snapshots.TryGetValue(symbol, out var snapshot)) continue;
            priceHistories.TryGetValue(symbol, out var history);
            history ??= [];

            // Build per-strategy previous values view using ConcurrentDictionary
            var strategyPrevValues = new Dictionary<string, decimal>();
            var allConditions = strategy.RootConditionGroup != null
                ? CollectConditions(strategy.RootConditionGroup)
                : strategy.Conditions;
            foreach (var condition in allConditions)
            {
                var key = $"{strategy.id}:{condition.ConditionId}:{symbol}";
                if (_previousValues.TryGetValue(key, out var prev))
                    strategyPrevValues[$"{condition.ConditionId}:{symbol}"] = prev;
            }

            // Evaluate conditions — use recursive group if available, else flat
            bool shouldTrigger;
            if (strategy.RootConditionGroup != null)
            {
                var (groupTriggered, groupResults) = conditionEvaluator.EvaluateGroup(
                    strategy.RootConditionGroup, snapshot, history, strategyPrevValues);
                shouldTrigger = groupTriggered;

                // Write back to persistent crossover state
                foreach (var (_, value, conditionId) in groupResults)
                {
                    var key = $"{strategy.id}:{conditionId}:{symbol}";
                    _previousValues[key] = value;
                }
            }
            else
            {
                var results = new List<(bool passed, decimal value)>();
                foreach (var condition in strategy.Conditions)
                {
                    var (triggered, currentValue) = conditionEvaluator.Evaluate(condition, snapshot, history, strategyPrevValues);
                    results.Add((triggered, currentValue));

                    // Write back to persistent crossover state
                    var key = $"{strategy.id}:{condition.ConditionId}:{symbol}";
                    _previousValues[key] = currentValue;
                }

                // Apply logic group
                shouldTrigger = strategy.LogicGroup switch
                {
                    LogicGroupType.And => results.Count > 0 && results.All(r => r.passed),
                    LogicGroupType.Or => results.Any(r => r.passed),
                    _ => false
                };
            }

            if (shouldTrigger)
            {
                logger.LogInformation("Strategy {StrategyName} ({StrategyId}) triggered for {Symbol}",
                    strategy.Name, strategy.id, symbol);
                var trades = await executionService.ExecuteActionsAsync(strategy, symbol, snapshot);
                anyTriggered = true;

                // Fan out to subscribers (best-effort — never blocks publisher trades)
                try
                {
                    await dispatchService.DispatchAsync(strategy, trades);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Subscription dispatch failed for strategy {StrategyId}", strategy.id);
                }
            }
        }

        // Update timestamps
        strategy.LastEvaluatedUtc = DateTime.UtcNow;
        if (anyTriggered)
            strategy.LastTriggeredUtc = DateTime.UtcNow;
        await cosmo.UpdateStrategyAsync(strategy);
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

            var history = await marketDataService.GetHistoricalPricesAsync(symbol);

            if (strategy.RootConditionGroup != null)
            {
                var (groupTriggered, groupResults) = evaluator.EvaluateGroup(
                    strategy.RootConditionGroup, snapshot, history, tempPreviousValues);
                perSymbolTriggered.Add(groupTriggered);

                // Look up condition metadata from the group tree for richer results
                var conditionMap = BuildConditionMap(strategy.RootConditionGroup);
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
                        Passed = passed
                    });
                }
            }
            else
            {
                var symbolResults = new List<bool>();
                foreach (var condition in strategy.Conditions)
                {
                    var (triggered, currentValue) = evaluator.Evaluate(condition, snapshot, history, tempPreviousValues);
                    symbolResults.Add(triggered);

                    result.Conditions.Add(new ConditionResult
                    {
                        ConditionId = condition.ConditionId,
                        ConditionType = condition.ConditionType,
                        CurrentValue = currentValue,
                        TargetValue = condition.Value,
                        Operator = condition.Operator,
                        Passed = triggered
                    });
                }

                var symbolTriggered = strategy.LogicGroup switch
                {
                    LogicGroupType.And => symbolResults.Count > 0 && symbolResults.All(r => r),
                    LogicGroupType.Or => symbolResults.Any(r => r),
                    _ => false
                };
                perSymbolTriggered.Add(symbolTriggered);
            }
        }

        // Any symbol triggering means the strategy would trigger
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
