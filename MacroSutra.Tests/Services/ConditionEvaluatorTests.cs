using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;

namespace MacroSutra.Tests.Services;

public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _evaluator = new();

    private static MarketSnapshot MakeSnapshot(string symbol = "AAPL", decimal price = 150m, long volume = 1_000_000, decimal changePercent = 1.5m) =>
        new() { Symbol = symbol, Price = price, Volume = volume, DailyChangePercent = changePercent };

    // ── Price conditions ──

    [Fact]
    public void Price_GreaterThan_True()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 100 };
        var (triggered, value) = _evaluator.Evaluate(condition, MakeSnapshot(price: 150), [], new());
        Assert.True(triggered);
        Assert.Equal(150m, value);
    }

    [Fact]
    public void Price_GreaterThan_False()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 200 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 150), [], new());
        Assert.False(triggered);
    }

    [Fact]
    public void Price_LessThan_True()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.LessThan, Value = 200 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 150), [], new());
        Assert.True(triggered);
    }

    [Fact]
    public void Price_Equal_True()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.Equal, Value = 150 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 150), [], new());
        Assert.True(triggered);
    }

    [Fact]
    public void Price_GreaterThanOrEqual_True()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThanOrEqual, Value = 150 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 150), [], new());
        Assert.True(triggered);
    }

    [Fact]
    public void Price_LessThanOrEqual_True()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.LessThanOrEqual, Value = 150 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 150), [], new());
        Assert.True(triggered);
    }

    // ── Volume conditions ──

    [Fact]
    public void Volume_GreaterThan_UsesVolumeField()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Volume, Operator = ConditionOperator.GreaterThan, Value = 500_000 };
        var (triggered, value) = _evaluator.Evaluate(condition, MakeSnapshot(volume: 1_000_000), [], new());
        Assert.True(triggered);
        Assert.Equal(1_000_000m, value);
    }

    // ── PercentChange conditions ──

    [Fact]
    public void PercentChange_GreaterThan_True()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.PercentChange, Operator = ConditionOperator.GreaterThan, Value = 1.0m };
        var (triggered, value) = _evaluator.Evaluate(condition, MakeSnapshot(changePercent: 2.5m), [], new());
        Assert.True(triggered);
        Assert.Equal(2.5m, value);
    }

    // ── MovingAverage conditions ──

    [Fact]
    public void MovingAverage_UsesSmaPeriod()
    {
        var prices = new decimal[] { 10, 11, 12, 13, 14 };
        var condition = new TriggerCondition { ConditionType = ConditionType.MovingAverage, Operator = ConditionOperator.GreaterThan, Value = 11, Period = 5 };
        var (triggered, value) = _evaluator.Evaluate(condition, MakeSnapshot(), prices, new());
        Assert.True(triggered);
        Assert.Equal(12m, value); // SMA(5) = 12
    }

    [Fact]
    public void MovingAverage_InsufficientData_ReturnsZero()
    {
        var prices = new decimal[] { 10, 11 };
        var condition = new TriggerCondition { ConditionType = ConditionType.MovingAverage, Operator = ConditionOperator.GreaterThan, Value = 5, Period = 20 };
        var (triggered, value) = _evaluator.Evaluate(condition, MakeSnapshot(), prices, new());
        Assert.False(triggered);
        Assert.Equal(0m, value);
    }

    // ── RSI conditions ──

    [Fact]
    public void RSI_AllGains_Returns100()
    {
        var prices = Enumerable.Range(1, 15).Select(i => (decimal)i).ToArray();
        var condition = new TriggerCondition { ConditionType = ConditionType.RSI, Operator = ConditionOperator.GreaterThan, Value = 70, Period = 14 };
        var (triggered, value) = _evaluator.Evaluate(condition, MakeSnapshot(), prices, new());
        Assert.True(triggered);
        Assert.Equal(100m, value);
    }

    // ── MACD conditions ──

    [Fact]
    public void MACD_UseMacdLine()
    {
        var prices = Enumerable.Range(1, 30).Select(i => (decimal)i).ToArray();
        var condition = new TriggerCondition { ConditionType = ConditionType.MACD, Operator = ConditionOperator.GreaterThan, Value = 0 };
        var (triggered, value) = _evaluator.Evaluate(condition, MakeSnapshot(), prices, new());
        Assert.True(triggered);
        Assert.True(value > 0);
    }

    // ── CrossesAbove / CrossesBelow ──

    [Fact]
    public void CrossesAbove_FirstEvaluation_ReturnsFalse()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.CrossesAbove, Value = 100 };
        var prev = new Dictionary<string, decimal>();
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 110), [], prev);
        Assert.False(triggered); // No previous value
    }

    [Fact]
    public void CrossesAbove_PreviousBelow_CurrentAbove_Triggers()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.CrossesAbove, Value = 100 };
        var prev = new Dictionary<string, decimal> { [$"{condition.ConditionId}:AAPL"] = 95 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 110), [], prev);
        Assert.True(triggered);
    }

    [Fact]
    public void CrossesAbove_PreviousAbove_CurrentAbove_DoesNotTrigger()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.CrossesAbove, Value = 100 };
        var prev = new Dictionary<string, decimal> { [$"{condition.ConditionId}:AAPL"] = 105 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 110), [], prev);
        Assert.False(triggered);
    }

    [Fact]
    public void CrossesBelow_PreviousAbove_CurrentBelow_Triggers()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.CrossesBelow, Value = 100 };
        var prev = new Dictionary<string, decimal> { [$"{condition.ConditionId}:AAPL"] = 105 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 90), [], prev);
        Assert.True(triggered);
    }

    [Fact]
    public void CrossesBelow_PreviousBelow_CurrentBelow_DoesNotTrigger()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.CrossesBelow, Value = 100 };
        var prev = new Dictionary<string, decimal> { [$"{condition.ConditionId}:AAPL"] = 90 };
        var (triggered, _) = _evaluator.Evaluate(condition, MakeSnapshot(price: 85), [], prev);
        Assert.False(triggered);
    }

    [Fact]
    public void Evaluate_UpdatesPreviousValues()
    {
        var condition = new TriggerCondition { ConditionType = ConditionType.Price, Operator = ConditionOperator.GreaterThan, Value = 100 };
        var prev = new Dictionary<string, decimal>();
        _evaluator.Evaluate(condition, MakeSnapshot(price: 150), [], prev);
        Assert.Equal(150m, prev[$"{condition.ConditionId}:AAPL"]);
    }
}
