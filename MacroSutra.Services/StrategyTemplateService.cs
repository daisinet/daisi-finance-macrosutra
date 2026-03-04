using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Services;

/// <summary>
/// Returns pre-built strategy templates. Templates are code-defined (no database needed).
/// </summary>
public class StrategyTemplateService
{
    private static readonly List<StrategyTemplate> Templates = new()
    {
        new StrategyTemplate
        {
            Id = "rsi-oversold",
            Name = "RSI Oversold Bounce",
            Description = "Buys when RSI drops below 30 (oversold) and sells when RSI rises above 70 (overbought). A classic mean-reversion strategy.",
            Category = "Mean Reversion",
            LogicGroup = LogicGroupType.And,
            Conditions = new List<TriggerCondition>
            {
                new() { ConditionType = ConditionType.RSI, Operator = ConditionOperator.LessThan, Value = 30, Period = 14 }
            },
            Actions = new List<TradeAction>
            {
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Buy, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 10 },
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Sell, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 100 }
            }
        },
        new StrategyTemplate
        {
            Id = "ma-crossover",
            Name = "Moving Average Crossover",
            Description = "Buys when price crosses above the 20-day moving average, sells when it crosses below. A trend-following strategy.",
            Category = "Trend Following",
            LogicGroup = LogicGroupType.And,
            Conditions = new List<TriggerCondition>
            {
                new() { ConditionType = ConditionType.MovingAverage, Operator = ConditionOperator.CrossesAbove, Value = 0, Period = 20 }
            },
            Actions = new List<TradeAction>
            {
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Buy, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 25 },
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Sell, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 100 }
            }
        },
        new StrategyTemplate
        {
            Id = "breakout",
            Name = "Price Breakout",
            Description = "Buys when price breaks above a target level with volume confirmation. Good for momentum plays on key resistance breaks.",
            Category = "Momentum",
            LogicGroup = LogicGroupType.And,
            Conditions = new List<TriggerCondition>
            {
                new() { ConditionType = ConditionType.Price, Operator = ConditionOperator.CrossesAbove, Value = 0 },
                new() { ConditionType = ConditionType.Volume, Operator = ConditionOperator.GreaterThan, Value = 1_000_000 }
            },
            Actions = new List<TradeAction>
            {
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Buy, QuantityType = QuantityType.DollarAmount, Quantity = 5000 }
            }
        },
        new StrategyTemplate
        {
            Id = "mean-reversion",
            Name = "Daily Mean Reversion",
            Description = "Buys after a significant daily drop (>3%) expecting a bounce. Sells after a recovery (>2% gain). Contrarian strategy.",
            Category = "Mean Reversion",
            LogicGroup = LogicGroupType.And,
            Conditions = new List<TriggerCondition>
            {
                new() { ConditionType = ConditionType.PercentChange, Operator = ConditionOperator.LessThan, Value = -3 }
            },
            Actions = new List<TradeAction>
            {
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Buy, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 15 },
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Sell, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 100 }
            }
        },
        new StrategyTemplate
        {
            Id = "momentum",
            Name = "MACD Momentum",
            Description = "Buys when the MACD line crosses above zero (bullish momentum), sells when it crosses below. Uses standard 12/26 MACD.",
            Category = "Momentum",
            LogicGroup = LogicGroupType.And,
            Conditions = new List<TriggerCondition>
            {
                new() { ConditionType = ConditionType.MACD, Operator = ConditionOperator.CrossesAbove, Value = 0 }
            },
            Actions = new List<TradeAction>
            {
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Buy, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 20 },
                new() { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Sell, QuantityType = QuantityType.PercentOfPortfolio, Quantity = 100 }
            }
        }
    };

    public List<StrategyTemplate> GetTemplates() => Templates.ToList();

    public StrategyTemplate? GetTemplate(string id) =>
        Templates.Find(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
