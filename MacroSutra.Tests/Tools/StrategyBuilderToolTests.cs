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
              "logicGroup": "And",
              "conditions": [
                { "conditionType": "RSI", "operator": "LessThan", "value": 30, "period": 14 }
              ],
              "actions": [
                { "actionType": "MarketOrder", "side": "Buy", "quantityType": "Shares", "quantity": 10 }
              ]
            }
            """;

        var result = StrategyBuilderTool.ParseStrategyJson(json);
        Assert.NotNull(result);
        Assert.Equal("RSI Dip Buy", result.Name);
        Assert.Equal(LogicGroupType.And, result.LogicGroup);
        Assert.Single(result.Conditions);
        Assert.Equal(ConditionType.RSI, result.Conditions[0].ConditionType);
        Assert.Equal(ConditionOperator.LessThan, result.Conditions[0].Operator);
        Assert.Equal(30m, result.Conditions[0].Value);
        Assert.Equal(14, result.Conditions[0].Period);
        Assert.Single(result.Actions);
        Assert.Equal(TradeActionType.MarketOrder, result.Actions[0].ActionType);
        Assert.Equal(TradeSide.Buy, result.Actions[0].Side);
        Assert.Equal(10m, result.Actions[0].Quantity);
    }

    [Fact]
    public void ParseStrategyJson_WithSurroundingText_ExtractsJson()
    {
        var content = """
            Here is the strategy:
            {
              "name": "Test",
              "logicGroup": "Or",
              "conditions": [
                { "conditionType": "Price", "operator": "GreaterThan", "value": 150 }
              ],
              "actions": [
                { "actionType": "Alert", "side": "Buy", "quantityType": "Shares", "quantity": 0 }
              ]
            }
            Let me know if you need changes.
            """;

        var result = StrategyBuilderTool.ParseStrategyJson(content);
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(LogicGroupType.Or, result.LogicGroup);
    }

    [Fact]
    public void ParseStrategyJson_NoConditions_ReturnsNull()
    {
        var json = """
            { "name": "Empty", "logicGroup": "And", "conditions": [], "actions": [{ "actionType": "Alert", "side": "Buy", "quantityType": "Shares", "quantity": 1 }] }
            """;

        Assert.Null(StrategyBuilderTool.ParseStrategyJson(json));
    }

    [Fact]
    public void ParseStrategyJson_NoActions_ReturnsNull()
    {
        var json = """
            { "name": "Empty", "logicGroup": "And", "conditions": [{ "conditionType": "Price", "operator": "GreaterThan", "value": 100 }], "actions": [] }
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
              "logicGroup": "And",
              "conditions": [
                { "conditionType": "Price", "operator": "LessThan", "value": 100 }
              ],
              "actions": [
                { "actionType": "LimitOrder", "side": "Buy", "quantityType": "Dollars", "quantity": 500, "limitPrice": 99.50, "stopPrice": 95.00 }
              ]
            }
            """;

        var result = StrategyBuilderTool.ParseStrategyJson(json);
        Assert.NotNull(result);
        Assert.Equal(99.50m, result.Actions[0].LimitPrice);
        Assert.Equal(95.00m, result.Actions[0].StopPrice);
    }
}
