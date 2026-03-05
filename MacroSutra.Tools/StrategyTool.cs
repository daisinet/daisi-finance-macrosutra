using System.Text.Json;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tools;

/// <summary>
/// Bot tool for querying and managing trading strategies.
/// </summary>
public class StrategyTool : DaisiToolBase
{
    private const string P_ACTION = "action";
    private const string P_ACCOUNT_ID = "accountId";
    private const string P_STRATEGY_ID = "strategyId";

    public override string Id => "macrosutra-strategy";
    public override string Name => "MacroSutra Strategy";

    public override string UseInstructions =>
        "Use this tool to query trading strategies and check trigger status. " +
        "Actions: \"list\" to list all strategies, \"get\" to get strategy details, " +
        "\"test\" to evaluate a strategy against live market data, \"templates\" to see available templates. " +
        "Keywords: strategy, strategies, triggers, conditions, trading rules, templates.";

    public override ToolParameter[] Parameters =>
    [
        new() { Name = P_ACTION, Description = "The action: \"list\", \"get\", \"test\", or \"templates\".", IsRequired = true },
        new() { Name = P_ACCOUNT_ID, Description = "The MacroSutra account ID.", IsRequired = true },
        new() { Name = P_STRATEGY_ID, Description = "The strategy ID (required for \"get\" and \"test\").", IsRequired = false }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var action = parameters.GetParameterValueOrDefault(P_ACTION);
        var accountId = parameters.GetParameterValueOrDefault(P_ACCOUNT_ID);
        var strategyId = parameters.GetParameter(P_STRATEGY_ID, false)?.Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Strategy: {action}",
            ExecutionTask = Task.Run(() => ExecuteAsync(toolContext, action, accountId, strategyId), cancellation)
        };
    }

    internal static async Task<ToolResult> ExecuteAsync(IToolContext context, string action, string accountId, string? strategyId)
    {
        try
        {
            return action.ToLowerInvariant() switch
            {
                "list" => await ListStrategiesAsync(context, accountId),
                "get" => await GetStrategyAsync(context, accountId, strategyId),
                "test" => await TestStrategyAsync(context, accountId, strategyId),
                "templates" => GetTemplates(context),
                _ => new ToolResult { Success = false, ErrorMessage = $"Unknown action: {action}. Use \"list\", \"get\", \"test\", or \"templates\"." }
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task<ToolResult> ListStrategiesAsync(IToolContext context, string accountId)
    {
        var service = context.Services.GetRequiredService<StrategyService>();
        var strategies = await service.GetStrategiesAsync(accountId);
        var output = strategies.Select(s => new
        {
            s.id, s.Name, s.Description, s.IsActive,
            Symbols = s.Symbols,
            ConditionCount = s.Conditions?.Count ?? 0,
            ActionCount = s.Actions?.Count ?? 0,
            s.LastEvaluatedUtc, s.LastTriggeredUtc
        });

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(output),
            OutputMessage = $"Found {strategies.Count} strategy(ies).",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static async Task<ToolResult> GetStrategyAsync(IToolContext context, string accountId, string? strategyId)
    {
        if (string.IsNullOrEmpty(strategyId))
            return new ToolResult { Success = false, ErrorMessage = "strategyId is required for the \"get\" action." };

        var service = context.Services.GetRequiredService<StrategyService>();
        var strategy = await service.GetStrategyAsync(strategyId, accountId);
        if (strategy == null)
            return new ToolResult { Success = false, ErrorMessage = $"Strategy '{strategyId}' not found." };

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(strategy),
            OutputMessage = $"Strategy: {strategy.Name}",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static async Task<ToolResult> TestStrategyAsync(IToolContext context, string accountId, string? strategyId)
    {
        if (string.IsNullOrEmpty(strategyId))
            return new ToolResult { Success = false, ErrorMessage = "strategyId is required for the \"test\" action." };

        var evalService = context.Services.GetRequiredService<StrategyEvaluationService>();
        var strategyService = context.Services.GetRequiredService<StrategyService>();
        var strategy = await strategyService.GetStrategyAsync(strategyId, accountId);
        if (strategy == null)
            return new ToolResult { Success = false, ErrorMessage = $"Strategy '{strategyId}' not found." };

        var result = await evalService.EvaluateSingleAsync(strategy);
        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(result),
            OutputMessage = $"Strategy '{strategy.Name}' evaluation complete.",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static ToolResult GetTemplates(IToolContext context)
    {
        var service = context.Services.GetRequiredService<StrategyTemplateService>();
        var templates = service.GetTemplates();
        var output = templates.Select(t => new
        {
            t.Id, t.Name, t.Description, t.Category,
            ConditionCount = t.Conditions?.Count ?? 0,
            ActionCount = t.Actions?.Count ?? 0
        });

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(output),
            OutputMessage = $"Found {templates.Count} template(s).",
            OutputFormat = InferenceOutputFormats.Json
        };
    }
}
