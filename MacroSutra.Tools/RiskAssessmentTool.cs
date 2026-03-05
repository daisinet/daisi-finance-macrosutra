using System.Text;
using System.Text.Json;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tools;

/// <summary>
/// AI-powered bot tool that evaluates strategy risk using inference.
/// Analyzes conditions, actions, historical performance, and portfolio exposure.
/// </summary>
public class RiskAssessmentTool : DaisiToolBase
{
    private const string P_ACCOUNT_ID = "accountId";
    private const string P_STRATEGY_ID = "strategyId";

    public override string Id => "macrosutra-risk-assessment";
    public override string Name => "MacroSutra Risk Assessment";

    public override string UseInstructions =>
        "Use this tool to get an AI-powered risk assessment of a trading strategy. " +
        "Analyzes strategy conditions, actions, historical performance, and portfolio exposure. " +
        "Returns a risk rating, key risks, and recommendations. " +
        "Keywords: risk, risk assessment, analyze risk, strategy risk, exposure, danger.";

    public override ToolParameter[] Parameters =>
    [
        new() { Name = P_ACCOUNT_ID, Description = "The MacroSutra account ID.", IsRequired = true },
        new() { Name = P_STRATEGY_ID, Description = "The strategy ID to assess.", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var accountId = parameters.GetParameterValueOrDefault(P_ACCOUNT_ID);
        var strategyId = parameters.GetParameterValueOrDefault(P_STRATEGY_ID);

        return new ToolExecutionContext
        {
            ExecutionMessage = "Assessing strategy risk...",
            ExecutionTask = Task.Run(() => ExecuteAsync(toolContext, accountId, strategyId), cancellation)
        };
    }

    internal static async Task<ToolResult> ExecuteAsync(IToolContext context, string accountId, string strategyId)
    {
        try
        {
            var strategyService = context.Services.GetRequiredService<StrategyService>();
            var strategy = await strategyService.GetStrategyAsync(strategyId, accountId);
            if (strategy == null)
                return new ToolResult { Success = false, ErrorMessage = $"Strategy '{strategyId}' not found." };

            // Gather context for AI analysis
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine($"Strategy: {strategy.Name}");
            contextBuilder.AppendLine($"Description: {strategy.Description}");
            contextBuilder.AppendLine($"Symbols: {string.Join(", ", strategy.Symbols)}");
            contextBuilder.AppendLine($"Logic: {strategy.LogicGroup}");
            contextBuilder.AppendLine($"Active: {strategy.IsActive}");

            contextBuilder.AppendLine("\nConditions:");
            foreach (var c in strategy.Conditions)
                contextBuilder.AppendLine($"  - {c.ConditionType} {c.Operator} {c.Value}{(c.Period.HasValue ? $" (period: {c.Period})" : "")}");

            contextBuilder.AppendLine("\nActions:");
            foreach (var a in strategy.Actions)
                contextBuilder.AppendLine($"  - {a.ActionType} {a.Side} {a.Quantity} {a.QuantityType}{(a.LimitPrice.HasValue ? $" limit: {a.LimitPrice}" : "")}{(a.StopPrice.HasValue ? $" stop: {a.StopPrice}" : "")}");

            // Add performance data if available
            try
            {
                var perfService = context.Services.GetRequiredService<StrategyPerformanceService>();
                var summary = await perfService.GetPerformanceSummaryAsync(accountId, strategyId);
                contextBuilder.AppendLine($"\nPerformance: {summary.TotalTriggers} triggers, {summary.WinRate:P0} win rate, {summary.TotalPnL:C} P&L");
            }
            catch { /* performance data optional */ }

            // Add position exposure if available
            try
            {
                var portfolioService = context.Services.GetRequiredService<PortfolioService>();
                var positions = await portfolioService.GetPositionsAsync(accountId);
                var relevant = positions.Where(p => strategy.Symbols.Contains(p.Symbol, StringComparer.OrdinalIgnoreCase)).ToList();
                if (relevant.Count > 0)
                {
                    contextBuilder.AppendLine("\nCurrent positions in strategy symbols:");
                    foreach (var p in relevant)
                        contextBuilder.AppendLine($"  - {p.Symbol}: {p.Quantity} shares @ {p.AverageCost:C}, current {p.CurrentPrice:C}, P&L {p.UnrealizedPnL:C}");
                }
            }
            catch { /* position data optional */ }

            var prompt = BuildPrompt(contextBuilder.ToString());
            var infRequest = SendInferenceRequest.CreateDefault();
            infRequest.Text = prompt;

            var infResult = await context.InferAsync(infRequest);

            return new ToolResult
            {
                Success = true,
                Output = infResult.Content,
                OutputMessage = $"Risk assessment for '{strategy.Name}' complete.",
                OutputFormat = InferenceOutputFormats.Markdown
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    internal static string BuildPrompt(string strategyContext)
    {
        return $"""
            You are a trading risk analyst. Analyze the following strategy and provide a concise risk assessment.

            {strategyContext}

            Provide your assessment in this format:

            ## Risk Rating
            Rate the strategy Low / Medium / High / Very High risk.

            ## Key Risks
            List the top 3-5 risks (concentration, volatility, lack of stop-loss, over-leverage, etc.).

            ## Strengths
            List any positive aspects of the strategy.

            ## Recommendations
            Suggest 2-3 specific improvements to reduce risk.

            Be concise and actionable. Focus on practical trading risks.
            """;
    }
}
