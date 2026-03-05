using System.Text.Json;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tools;

/// <summary>
/// Bot tool for running backtests and retrieving results.
/// </summary>
public class BacktestTool : DaisiToolBase
{
    private const string P_ACTION = "action";
    private const string P_ACCOUNT_ID = "accountId";
    private const string P_STRATEGY_ID = "strategyId";
    private const string P_SYMBOL = "symbol";
    private const string P_FROM_DATE = "fromDate";
    private const string P_TO_DATE = "toDate";

    public override string Id => "macrosutra-backtest";
    public override string Name => "MacroSutra Backtest";

    public override string UseInstructions =>
        "Use this tool to run backtests against historical data or view past results. " +
        "Actions: \"run\" to start a new backtest, \"list\" to see past backtests, " +
        "\"get\" to get detailed results of a specific backtest. " +
        "Keywords: backtest, historical, simulate, test strategy, Sharpe, drawdown.";

    public override ToolParameter[] Parameters =>
    [
        new() { Name = P_ACTION, Description = "The action: \"run\", \"list\", or \"get\".", IsRequired = true },
        new() { Name = P_ACCOUNT_ID, Description = "The MacroSutra account ID.", IsRequired = true },
        new() { Name = P_STRATEGY_ID, Description = "The strategy ID (required for \"run\").", IsRequired = false },
        new() { Name = P_SYMBOL, Description = "The symbol to backtest (required for \"run\"). Example: \"AAPL\".", IsRequired = false },
        new() { Name = P_FROM_DATE, Description = "Start date in yyyy-MM-dd format (required for \"run\").", IsRequired = false },
        new() { Name = P_TO_DATE, Description = "End date in yyyy-MM-dd format (required for \"run\").", IsRequired = false }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var action = parameters.GetParameterValueOrDefault(P_ACTION);
        var accountId = parameters.GetParameterValueOrDefault(P_ACCOUNT_ID);
        var strategyId = parameters.GetParameter(P_STRATEGY_ID, false)?.Value;
        var symbol = parameters.GetParameter(P_SYMBOL, false)?.Value;
        var fromDate = parameters.GetParameter(P_FROM_DATE, false)?.Value;
        var toDate = parameters.GetParameter(P_TO_DATE, false)?.Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Backtest: {action}",
            ExecutionTask = Task.Run(() => ExecuteAsync(toolContext, action, accountId, strategyId, symbol, fromDate, toDate), cancellation)
        };
    }

    internal static async Task<ToolResult> ExecuteAsync(IToolContext context, string action, string accountId,
        string? strategyId, string? symbol, string? fromDate, string? toDate)
    {
        try
        {
            var service = context.Services.GetRequiredService<BacktestService>();

            return action.ToLowerInvariant() switch
            {
                "run" => await RunBacktestAsync(service, accountId, strategyId, symbol, fromDate, toDate),
                "list" => await ListBacktestsAsync(service, accountId, strategyId),
                "get" => await GetBacktestAsync(service, accountId, strategyId),
                _ => new ToolResult { Success = false, ErrorMessage = $"Unknown action: {action}. Use \"run\", \"list\", or \"get\"." }
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task<ToolResult> RunBacktestAsync(BacktestService service, string accountId,
        string? strategyId, string? symbol, string? fromDate, string? toDate)
    {
        if (string.IsNullOrEmpty(strategyId))
            return new ToolResult { Success = false, ErrorMessage = "strategyId is required." };
        if (string.IsNullOrEmpty(symbol))
            return new ToolResult { Success = false, ErrorMessage = "symbol is required." };
        if (!DateOnly.TryParse(fromDate, out var from))
            return new ToolResult { Success = false, ErrorMessage = "fromDate must be in yyyy-MM-dd format." };
        if (!DateOnly.TryParse(toDate, out var to))
            return new ToolResult { Success = false, ErrorMessage = "toDate must be in yyyy-MM-dd format." };

        var result = await service.CreateAndRunBacktestAsync(
            strategyId, accountId, "", symbol.ToUpperInvariant(), from, to, 100_000m);

        var output = new
        {
            result.id,
            result.StrategyName,
            result.Symbol,
            result.StartDate,
            result.EndDate,
            Status = result.Status.ToString(),
            Metrics = result.Metrics != null ? new
            {
                result.Metrics.TotalReturnPercent,
                result.Metrics.SharpeRatio,
                result.Metrics.MaxDrawdownPercent,
                result.Metrics.WinRate,
                result.Metrics.TotalTrades,
                result.Metrics.ProfitFactor
            } : null,
            TradeCount = result.Trades?.Count ?? 0
        };

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(output),
            OutputMessage = $"Backtest complete: {result.Metrics?.TotalReturnPercent:F1}% return, {result.Metrics?.SharpeRatio:F2} Sharpe.",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static async Task<ToolResult> ListBacktestsAsync(BacktestService service, string accountId, string? strategyId)
    {
        var backtests = await service.GetBacktestsAsync(accountId, strategyId);
        var output = backtests.Select(b => new
        {
            b.id, b.StrategyName, b.Symbol,
            b.StartDate, b.EndDate,
            Status = b.Status.ToString(),
            Return = b.Metrics?.TotalReturnPercent,
            Sharpe = b.Metrics?.SharpeRatio
        });

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(output),
            OutputMessage = $"Found {backtests.Count} backtest(s).",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static async Task<ToolResult> GetBacktestAsync(BacktestService service, string accountId, string? backtestId)
    {
        if (string.IsNullOrEmpty(backtestId))
            return new ToolResult { Success = false, ErrorMessage = "strategyId parameter should contain the backtest ID for \"get\"." };

        var result = await service.GetBacktestAsync(backtestId, accountId);
        if (result == null)
            return new ToolResult { Success = false, ErrorMessage = $"Backtest '{backtestId}' not found." };

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(result),
            OutputMessage = $"Backtest: {result.StrategyName} on {result.Symbol}",
            OutputFormat = InferenceOutputFormats.Json
        };
    }
}
