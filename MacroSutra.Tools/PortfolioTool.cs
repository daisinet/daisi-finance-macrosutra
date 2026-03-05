using System.Text.Json;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tools;

/// <summary>
/// Bot tool for querying portfolio data — positions, balances, and brokerage accounts.
/// </summary>
public class PortfolioTool : DaisiToolBase
{
    private const string P_ACTION = "action";
    private const string P_ACCOUNT_ID = "accountId";
    private const string P_BROKERAGE_ACCOUNT_ID = "brokerageAccountId";

    public override string Id => "macrosutra-portfolio";
    public override string Name => "MacroSutra Portfolio";

    public override string UseInstructions =>
        "Use this tool to query the user's trading portfolio. " +
        "Actions: \"positions\" to get current positions, \"balance\" to get account balances, " +
        "\"accounts\" to list linked brokerage accounts. " +
        "Keywords: portfolio, positions, holdings, balance, brokerage, accounts.";

    public override ToolParameter[] Parameters =>
    [
        new() { Name = P_ACTION, Description = "The action: \"positions\", \"balance\", or \"accounts\".", IsRequired = true },
        new() { Name = P_ACCOUNT_ID, Description = "The MacroSutra account ID.", IsRequired = true },
        new() { Name = P_BROKERAGE_ACCOUNT_ID, Description = "Optional brokerage account ID to filter positions.", IsRequired = false }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var action = parameters.GetParameterValueOrDefault(P_ACTION);
        var accountId = parameters.GetParameterValueOrDefault(P_ACCOUNT_ID);
        var brokerageAccountId = parameters.GetParameter(P_BROKERAGE_ACCOUNT_ID, false)?.Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Querying portfolio: {action}",
            ExecutionTask = Task.Run(() => ExecuteAsync(toolContext, action, accountId, brokerageAccountId), cancellation)
        };
    }

    internal static async Task<ToolResult> ExecuteAsync(IToolContext context, string action, string accountId, string? brokerageAccountId)
    {
        try
        {
            var portfolioService = context.Services.GetRequiredService<PortfolioService>();

            return action.ToLowerInvariant() switch
            {
                "positions" => await GetPositionsAsync(portfolioService, accountId, brokerageAccountId),
                "balance" or "balances" => await GetBalancesAsync(portfolioService, accountId),
                "accounts" => await GetAccountsAsync(portfolioService, accountId),
                _ => new ToolResult { Success = false, ErrorMessage = $"Unknown action: {action}. Use \"positions\", \"balance\", or \"accounts\"." }
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task<ToolResult> GetPositionsAsync(PortfolioService service, string accountId, string? brokerageAccountId)
    {
        var positions = await service.GetPositionsAsync(accountId, brokerageAccountId);
        var output = positions.Select(p => new
        {
            p.Symbol,
            p.Quantity,
            p.AverageCost,
            p.CurrentPrice,
            MarketValue = p.MarketValue,
            UnrealizedPnL = p.UnrealizedPnL
        });

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(output),
            OutputMessage = $"Found {positions.Count} position(s).",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static async Task<ToolResult> GetBalancesAsync(PortfolioService service, string accountId)
    {
        var accounts = await service.GetBrokerageAccountsAsync(accountId, activeOnly: true);
        var balances = accounts.Select(a => new
        {
            a.Name,
            Provider = a.Provider.ToString(),
            a.CachedBalance,
            a.IsPaperTrading,
            LastSync = a.LastSyncUtc?.ToString("yyyy-MM-dd HH:mm")
        });

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(balances),
            OutputMessage = $"Found {accounts.Count} account(s).",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static async Task<ToolResult> GetAccountsAsync(PortfolioService service, string accountId)
    {
        var accounts = await service.GetBrokerageAccountsAsync(accountId);
        var output = accounts.Select(a => new
        {
            a.id,
            a.Name,
            Provider = a.Provider.ToString(),
            a.IsPaperTrading,
            a.IsActive,
            a.CachedBalance,
            LastSync = a.LastSyncUtc?.ToString("yyyy-MM-dd HH:mm")
        });

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(output),
            OutputMessage = $"Found {accounts.Count} brokerage account(s).",
            OutputFormat = InferenceOutputFormats.Json
        };
    }
}
