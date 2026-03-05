using System.Text.Json;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tools;

/// <summary>
/// Bot tool for querying strategy performance and trigger history.
/// </summary>
public class PerformanceTool : DaisiToolBase
{
    private const string P_ACTION = "action";
    private const string P_ACCOUNT_ID = "accountId";
    private const string P_STRATEGY_ID = "strategyId";

    public override string Id => "macrosutra-performance";
    public override string Name => "MacroSutra Performance";

    public override string UseInstructions =>
        "Use this tool to get strategy performance metrics and trigger history. " +
        "Actions: \"summary\" for performance summary (win rate, P&L, Sharpe), " +
        "\"triggers\" for recent trigger history with outcomes. " +
        "Keywords: performance, win rate, P&L, returns, triggers, outcomes.";

    public override ToolParameter[] Parameters =>
    [
        new() { Name = P_ACTION, Description = "The action: \"summary\" or \"triggers\".", IsRequired = true },
        new() { Name = P_ACCOUNT_ID, Description = "The MacroSutra account ID.", IsRequired = true },
        new() { Name = P_STRATEGY_ID, Description = "The strategy ID.", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var action = parameters.GetParameterValueOrDefault(P_ACTION);
        var accountId = parameters.GetParameterValueOrDefault(P_ACCOUNT_ID);
        var strategyId = parameters.GetParameterValueOrDefault(P_STRATEGY_ID);

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Performance: {action}",
            ExecutionTask = Task.Run(() => ExecuteAsync(toolContext, action, accountId, strategyId), cancellation)
        };
    }

    internal static async Task<ToolResult> ExecuteAsync(IToolContext context, string action, string accountId, string strategyId)
    {
        try
        {
            var service = context.Services.GetRequiredService<StrategyPerformanceService>();

            return action.ToLowerInvariant() switch
            {
                "summary" => await GetSummaryAsync(service, accountId, strategyId),
                "triggers" => await GetTriggersAsync(service, accountId, strategyId),
                _ => new ToolResult { Success = false, ErrorMessage = $"Unknown action: {action}. Use \"summary\" or \"triggers\"." }
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task<ToolResult> GetSummaryAsync(StrategyPerformanceService service, string accountId, string strategyId)
    {
        var summary = await service.GetPerformanceSummaryAsync(accountId, strategyId);
        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(summary),
            OutputMessage = $"Performance summary: {summary.TotalTriggers} triggers, {summary.WinRate:P0} win rate.",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static async Task<ToolResult> GetTriggersAsync(StrategyPerformanceService service, string accountId, string strategyId)
    {
        var triggers = await service.GetTriggerHistoryAsync(accountId, strategyId);
        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(triggers.Take(50)),
            OutputMessage = $"Found {triggers.Count} trigger record(s).",
            OutputFormat = InferenceOutputFormats.Json
        };
    }
}
