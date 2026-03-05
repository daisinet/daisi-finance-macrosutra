using System.Text.Json;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tools;

/// <summary>
/// AI-powered bot tool that converts natural language descriptions into trading strategies.
/// Uses inference to parse user intent into structured trigger groups with conditions and actions.
/// </summary>
public class StrategyBuilderTool : DaisiToolBase
{
    private const string P_ACCOUNT_ID = "accountId";
    private const string P_DESCRIPTION = "description";
    private const string P_SYMBOLS = "symbols";

    public override string Id => "macrosutra-strategy-builder";
    public override string Name => "MacroSutra Strategy Builder";

    public override string UseInstructions =>
        "Use this tool to create a trading strategy from a natural language description. " +
        "The AI will convert the description into structured trigger groups with conditions and actions. " +
        "Example: \"Buy AAPL when RSI drops below 30 and sell when RSI goes above 70\". " +
        "Keywords: create strategy, build strategy, new strategy, natural language, auto-create.";

    public override ToolParameter[] Parameters =>
    [
        new() { Name = P_ACCOUNT_ID, Description = "The MacroSutra account ID.", IsRequired = true },
        new() { Name = P_DESCRIPTION, Description = "Natural language description of the trading strategy.", IsRequired = true },
        new() { Name = P_SYMBOLS, Description = "Comma-separated symbols (e.g. \"AAPL,MSFT\"). Required.", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var accountId = parameters.GetParameterValueOrDefault(P_ACCOUNT_ID);
        var description = parameters.GetParameterValueOrDefault(P_DESCRIPTION);
        var symbols = parameters.GetParameterValueOrDefault(P_SYMBOLS);

        return new ToolExecutionContext
        {
            ExecutionMessage = "Building strategy from description...",
            ExecutionTask = Task.Run(() => ExecuteAsync(toolContext, accountId, description, symbols), cancellation)
        };
    }

    internal static async Task<ToolResult> ExecuteAsync(IToolContext context, string accountId, string description, string symbols)
    {
        try
        {
            var symbolList = symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant()).ToList();

            if (symbolList.Count == 0)
                return new ToolResult { Success = false, ErrorMessage = "At least one symbol is required." };

            var prompt = BuildPrompt(description, symbolList);
            var infRequest = SendInferenceRequest.CreateDefault();
            infRequest.Text = prompt;

            var infResult = await context.InferAsync(infRequest);
            var parsed = ParseStrategyJson(infResult.Content);
            if (parsed == null)
            {
                return new ToolResult
                {
                    Success = true,
                    Output = infResult.Content,
                    OutputMessage = "AI generated strategy suggestions (could not auto-parse into structured format).",
                    OutputFormat = InferenceOutputFormats.Markdown
                };
            }

            // Build the strategy from parsed output
            var strategy = new TradingStrategy
            {
                AccountId = accountId,
                Name = parsed.Name ?? $"Strategy for {string.Join(", ", symbolList)}",
                Description = description,
                Symbols = symbolList,
                TriggerGroups = parsed.TriggerGroups,
                IsActive = false // user must activate manually
            };

            var service = context.Services.GetRequiredService<StrategyService>();
            strategy = await service.CreateStrategyAsync(strategy);

            var totalConditions = strategy.TriggerGroups.Sum(tg => tg.Conditions.Conditions.Count);
            var totalActions = strategy.TriggerGroups.Sum(tg => tg.Actions.Count);

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(new
                {
                    strategy.id,
                    strategy.Name,
                    strategy.Description,
                    strategy.Symbols,
                    TriggerGroupCount = strategy.TriggerGroups.Count,
                    TotalConditions = totalConditions,
                    TotalActions = totalActions,
                    strategy.IsActive
                }),
                OutputMessage = $"Created strategy '{strategy.Name}' with {strategy.TriggerGroups.Count} trigger group(s) ({totalConditions} condition(s), {totalActions} action(s)). Activate it when ready.",
                OutputFormat = InferenceOutputFormats.Json
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    internal static string BuildPrompt(string description, List<string> symbols)
    {
        return $$"""
            You are a trading strategy builder. Convert the user's natural language description into a structured trading strategy with trigger groups.
            Each trigger group is an independent set of conditions and actions. For example, a buy signal and a sell signal should be separate trigger groups.

            Available condition types: {{string.Join(", ", Enum.GetNames<ConditionType>())}}
            Available operators: {{string.Join(", ", Enum.GetNames<ConditionOperator>())}}
            Available action types: {{string.Join(", ", Enum.GetNames<TradeActionType>())}}
            Available trade sides: Buy, Sell
            Available quantity types: Shares, Dollars, Percent

            Symbols: {{string.Join(", ", symbols)}}
            User description: {{description}}

            Respond with ONLY a JSON object in this exact format:
            {
              "name": "strategy name",
              "triggerGroups": [
                {
                  "name": "Buy Signal",
                  "logic": "And",
                  "conditions": [
                    {
                      "conditionType": "Price|Volume|PercentChange|MovingAverage|RSI|MACD|TimeOfDay|DayOfWeek",
                      "operator": "GreaterThan|LessThan|CrossesAbove|CrossesBelow|Equal|GreaterThanOrEqual|LessThanOrEqual",
                      "value": 0.0,
                      "period": null
                    }
                  ],
                  "actions": [
                    {
                      "actionType": "MarketOrder|LimitOrder|StopOrder|Alert",
                      "side": "Buy|Sell",
                      "quantityType": "Shares|Dollars|Percent",
                      "quantity": 0.0,
                      "limitPrice": null,
                      "stopPrice": null
                    }
                  ]
                }
              ]
            }
            """;
    }

    internal static ParsedStrategy? ParseStrategyJson(string content)
    {
        try
        {
            // Find JSON object in the response
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;

            var json = content[start..(end + 1)];
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new ParsedStrategy
            {
                Name = root.TryGetProperty("name", out var n) ? n.GetString() : null
            };

            if (root.TryGetProperty("triggerGroups", out var groups))
            {
                foreach (var g in groups.EnumerateArray())
                {
                    var tg = new TriggerGroup();

                    if (g.TryGetProperty("name", out var gn))
                        tg.Name = gn.GetString() ?? "";

                    if (g.TryGetProperty("interval", out var interval) && Enum.TryParse<BarTimeFrame>(interval.GetString(), true, out var ivVal))
                        tg.Interval = ivVal;

                    if (g.TryGetProperty("logic", out var logic) && Enum.TryParse<LogicGroupType>(logic.GetString(), true, out var lgt))
                        tg.Conditions.Logic = lgt;

                    if (g.TryGetProperty("conditions", out var conditions))
                    {
                        foreach (var c in conditions.EnumerateArray())
                        {
                            var cond = new TriggerCondition();
                            if (c.TryGetProperty("conditionType", out var ct) && Enum.TryParse<ConditionType>(ct.GetString(), true, out var ctVal))
                                cond.ConditionType = ctVal;
                            if (c.TryGetProperty("operator", out var op) && Enum.TryParse<ConditionOperator>(op.GetString(), true, out var opVal))
                                cond.Operator = opVal;
                            if (c.TryGetProperty("value", out var v))
                                cond.Value = v.GetDecimal();
                            if (c.TryGetProperty("period", out var p) && p.ValueKind == JsonValueKind.Number)
                                cond.Period = p.GetInt32();
                            tg.Conditions.Conditions.Add(cond);
                        }
                    }

                    if (g.TryGetProperty("actions", out var actions))
                    {
                        foreach (var a in actions.EnumerateArray())
                        {
                            var action = new TradeAction();
                            if (a.TryGetProperty("actionType", out var at) && Enum.TryParse<TradeActionType>(at.GetString(), true, out var atVal))
                                action.ActionType = atVal;
                            if (a.TryGetProperty("side", out var s) && Enum.TryParse<TradeSide>(s.GetString(), true, out var sVal))
                                action.Side = sVal;
                            if (a.TryGetProperty("quantityType", out var qt) && Enum.TryParse<QuantityType>(qt.GetString(), true, out var qtVal))
                                action.QuantityType = qtVal;
                            if (a.TryGetProperty("quantity", out var q))
                                action.Quantity = q.GetDecimal();
                            if (a.TryGetProperty("limitPrice", out var lp) && lp.ValueKind == JsonValueKind.Number)
                                action.LimitPrice = lp.GetDecimal();
                            if (a.TryGetProperty("stopPrice", out var sp) && sp.ValueKind == JsonValueKind.Number)
                                action.StopPrice = sp.GetDecimal();
                            tg.Actions.Add(action);
                        }
                    }

                    if (tg.Conditions.Conditions.Count > 0 && tg.Actions.Count > 0)
                        result.TriggerGroups.Add(tg);
                }
            }

            return result.TriggerGroups.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }

    internal class ParsedStrategy
    {
        public string? Name { get; set; }
        public List<TriggerGroup> TriggerGroups { get; set; } = new();
    }
}
