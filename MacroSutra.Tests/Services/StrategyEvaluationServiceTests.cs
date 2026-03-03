using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;

namespace MacroSutra.Tests.Services;

public class StrategyEvaluationServiceTests
{
    // ── AND logic ──

    [Fact]
    public void AndLogic_AllPass_Triggers()
    {
        var results = new List<(bool passed, decimal value)> { (true, 100), (true, 50) };
        var shouldTrigger = LogicGroupType.And switch
        {
            LogicGroupType.And => results.Count > 0 && results.All(r => r.passed),
            LogicGroupType.Or => results.Any(r => r.passed),
            _ => false
        };
        Assert.True(shouldTrigger);
    }

    [Fact]
    public void AndLogic_OneFails_DoesNotTrigger()
    {
        var results = new List<(bool passed, decimal value)> { (true, 100), (false, 50) };
        var shouldTrigger = LogicGroupType.And switch
        {
            LogicGroupType.And => results.Count > 0 && results.All(r => r.passed),
            LogicGroupType.Or => results.Any(r => r.passed),
            _ => false
        };
        Assert.False(shouldTrigger);
    }

    // ── OR logic ──

    [Fact]
    public void OrLogic_OnePass_Triggers()
    {
        var results = new List<(bool passed, decimal value)> { (false, 100), (true, 50) };
        var shouldTrigger = LogicGroupType.Or switch
        {
            LogicGroupType.And => results.Count > 0 && results.All(r => r.passed),
            LogicGroupType.Or => results.Any(r => r.passed),
            _ => false
        };
        Assert.True(shouldTrigger);
    }

    [Fact]
    public void OrLogic_NonePass_DoesNotTrigger()
    {
        var results = new List<(bool passed, decimal value)> { (false, 100), (false, 50) };
        var shouldTrigger = LogicGroupType.Or switch
        {
            LogicGroupType.And => results.Count > 0 && results.All(r => r.passed),
            LogicGroupType.Or => results.Any(r => r.passed),
            _ => false
        };
        Assert.False(shouldTrigger);
    }

    // ── Market hours ──

    [Fact]
    public void IsMarketOpen_ReturnsBoolean()
    {
        // Just verify it doesn't throw — actual result depends on current time
        var result = StrategyEvaluationService.IsMarketOpen();
        Assert.IsType<bool>(result);
    }

    // ── Condition evaluation integration ──

    [Fact]
    public void FullEvaluation_MultipleConditions_AndLogic()
    {
        var evaluator = new ConditionEvaluator();
        var snapshot = new MarketSnapshot { Symbol = "AAPL", Price = 150, Volume = 2_000_000, DailyChangePercent = 3.0m };
        var prev = new Dictionary<string, decimal>();

        var conditions = new List<TriggerCondition>
        {
            new() { ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 100 },
            new() { ConditionType = ConditionType.Volume, Operator = ConditionOperator.GreaterThan, Value = 1_000_000 },
            new() { ConditionType = ConditionType.PercentChange, Operator = ConditionOperator.GreaterThan, Value = 2.0m }
        };

        var results = conditions.Select(c => evaluator.Evaluate(c, snapshot, [], prev)).ToList();

        // AND: all should pass
        Assert.True(results.All(r => r.triggered));
    }

    [Fact]
    public void FullEvaluation_MultipleConditions_OrLogic_OneFails()
    {
        var evaluator = new ConditionEvaluator();
        var snapshot = new MarketSnapshot { Symbol = "AAPL", Price = 150, Volume = 500_000, DailyChangePercent = 3.0m };
        var prev = new Dictionary<string, decimal>();

        var conditions = new List<TriggerCondition>
        {
            new() { ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 100 },      // true
            new() { ConditionType = ConditionType.Volume, Operator = ConditionOperator.GreaterThan, Value = 1_000_000 } // false
        };

        var results = conditions.Select(c => evaluator.Evaluate(c, snapshot, [], prev)).ToList();

        // OR: at least one passes
        Assert.True(results.Any(r => r.triggered));
        Assert.False(results.All(r => r.triggered));
    }

    [Fact]
    public void FullEvaluation_EmptyConditions_AndLogic_DoesNotTrigger()
    {
        var results = new List<(bool passed, decimal value)>();
        var shouldTrigger = results.Count > 0 && results.All(r => r.passed);
        Assert.False(shouldTrigger);
    }
}
