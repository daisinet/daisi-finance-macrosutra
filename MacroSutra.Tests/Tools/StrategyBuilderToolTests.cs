using MacroSutra.Core.Enums;
using MacroSutra.Tools;

namespace MacroSutra.Tests.Tools;

public class StrategyBuilderToolTests
{
    // ── Metadata ──

    [Fact]
    public void Id_IsCorrect() => Assert.Equal("macrosutra-strategy-builder", new StrategyBuilderTool().Id);

    // ── BuildPrompt ──

    [Fact]
    public void BuildPrompt_ContainsDescription()
    {
        var prompt = StrategyBuilderTool.BuildPrompt("Buy when RSI below 30", ["AAPL"]);
        Assert.Contains("Buy when RSI below 30", prompt);
        Assert.Contains("AAPL", prompt);
    }

    [Fact]
    public void BuildPrompt_ContainsEnumValues()
    {
        var prompt = StrategyBuilderTool.BuildPrompt("test", ["SPY"]);
        Assert.Contains("RSI", prompt);
        Assert.Contains("CrossesAbove", prompt);
        Assert.Contains("MarketOrder", prompt);
    }

    // ── ParseStrategyJson ──

    [Fact]
    public void ParseStrategyJson_ValidJson_ReturnsStrategy()
    {
        var json = """
            {
              "name": "RSI Dip Buy",
              "triggerGroups": [
                {
                  "name": "Buy Signal",
                  "logic": "And",
                  "conditions": [
                    { "conditionType": "RSI", "operator": "LessThan", "value": 30, "period": 14 }
                  ],
                  "actions": [
                    { "actionType": "MarketOrder", "side": "Buy", "quantityType": "Shares", "quantity": 10 }
                  ]
                }
              ]
            }
            """;

        var result = StrategyBuilderTool.ParseStrategyJson(json);
        Assert.NotNull(result);
        Assert.Equal("RSI Dip Buy", result.Name);
        Assert.Single(result.TriggerGroups);
        var group = result.TriggerGroups[0];
        Assert.Equal("Buy Signal", group.Name);
        Assert.Single(group.Conditions.Conditions);
        Assert.Equal(ConditionType.RSI, group.Conditions.Conditions[0].ConditionType);
        Assert.Equal(ConditionOperator.LessThan, group.Conditions.Conditions[0].Operator);
        Assert.Equal(30m, group.Conditions.Conditions[0].Value);
        Assert.Equal(14, group.Conditions.Conditions[0].Period);
        Assert.Single(group.Actions);
        Assert.Equal(TradeActionType.MarketOrder, group.Actions[0].ActionType);
        Assert.Equal(TradeSide.Buy, group.Actions[0].Side);
        Assert.Equal(10m, group.Actions[0].Quantity);
    }

    [Fact]
    public void ParseStrategyJson_MultipleTriggerGroups_ParsesAll()
    {
        var json = """
            {
              "name": "RSI Strategy",
              "triggerGroups": [
                {
                  "name": "Buy Signal",
                  "logic": "And",
                  "conditions": [{ "conditionType": "RSI", "operator": "LessThan", "value": 30 }],
                  "actions": [{ "actionType": "MarketOrder", "side": "Buy", "quantityType": "Shares", "quantity": 10 }]
                },
                {
                  "name": "Sell Signal",
                  "logic": "Or",
                  "conditions": [{ "conditionType": "RSI", "operator": "GreaterThan", "value": 70 }],
                  "actions": [{ "actionType": "MarketOrder", "side": "Sell", "quantityType": "Shares", "quantity": 10 }]
                }
              ]
            }
            """;

        var result = StrategyBuilderTool.ParseStrategyJson(json);
        Assert.NotNull(result);
        Assert.Equal(2, result.TriggerGroups.Count);
        Assert.Equal("Buy Signal", result.TriggerGroups[0].Name);
        Assert.Equal("Sell Signal", result.TriggerGroups[1].Name);
        Assert.Equal(LogicGroupType.Or, result.TriggerGroups[1].Conditions.Logic);
    }

    [Fact]
    public void ParseStrategyJson_WithSurroundingText_ExtractsJson()
    {
        var content = """
            Here is the strategy:
            {
              "name": "Test",
              "triggerGroups": [
                {
                  "name": "Entry",
                  "logic": "Or",
                  "conditions": [{ "conditionType": "Price", "operator": "GreaterThan", "value": 150 }],
                  "actions": [{ "actionType": "Alert", "side": "Buy", "quantityType": "Shares", "quantity": 0 }]
                }
              ]
            }
            Let me know if you need changes.
            """;

        var result = StrategyBuilderTool.ParseStrategyJson(content);
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Single(result.TriggerGroups);
    }

    [Fact]
    public void ParseStrategyJson_NoTriggerGroups_ReturnsNull()
    {
        var json = """
            { "name": "Empty", "triggerGroups": [] }
            """;

        Assert.Null(StrategyBuilderTool.ParseStrategyJson(json));
    }

    [Fact]
    public void ParseStrategyJson_EmptyConditions_SkipsGroup()
    {
        var json = """
            {
              "name": "Empty",
              "triggerGroups": [
                {
                  "name": "Empty Group",
                  "logic": "And",
                  "conditions": [],
                  "actions": [{ "actionType": "Alert", "side": "Buy", "quantityType": "Shares", "quantity": 1 }]
                }
              ]
            }
            """;

        Assert.Null(StrategyBuilderTool.ParseStrategyJson(json));
    }

    [Fact]
    public void ParseStrategyJson_InvalidJson_ReturnsNull()
    {
        Assert.Null(StrategyBuilderTool.ParseStrategyJson("not json at all"));
    }

    [Fact]
    public void ParseStrategyJson_NoJsonObject_ReturnsNull()
    {
        Assert.Null(StrategyBuilderTool.ParseStrategyJson("just some text with no braces"));
    }

    [Fact]
    public void ParseStrategyJson_WithLimitAndStopPrices_ParsesThem()
    {
        var json = """
            {
              "name": "Limit Order",
              "triggerGroups": [
                {
                  "name": "Entry",
                  "logic": "And",
                  "conditions": [{ "conditionType": "Price", "operator": "LessThan", "value": 100 }],
                  "actions": [{ "actionType": "LimitOrder", "side": "Buy", "quantityType": "Dollars", "quantity": 500, "limitPrice": 99.50, "stopPrice": 95.00 }]
                }
              ]
            }
            """;

        var result = StrategyBuilderTool.ParseStrategyJson(json);
        Assert.NotNull(result);
        Assert.Equal(99.50m, result.TriggerGroups[0].Actions[0].LimitPrice);
        Assert.Equal(95.00m, result.TriggerGroups[0].Actions[0].StopPrice);
    }
}
