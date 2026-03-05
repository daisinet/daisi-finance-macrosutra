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
/// Bot tool for placing trades and querying trade history.
/// </summary>
public class TradeTool : DaisiToolBase
{
    private const string P_ACTION = "action";
    private const string P_ACCOUNT_ID = "accountId";
    private const string P_SYMBOL = "symbol";
    private const string P_SIDE = "side";
    private const string P_QUANTITY = "quantity";
    private const string P_ORDER_ID = "orderId";

    public override string Id => "macrosutra-trade";
    public override string Name => "MacroSutra Trade";

    public override string UseInstructions =>
        "Use this tool to place trades or query trade history. " +
        "Actions: \"buy\" to place a buy order, \"sell\" to place a sell order, " +
        "\"status\" to check an order's status, \"history\" to get recent trades. " +
        "IMPORTANT: Always confirm with the user before placing a trade. " +
        "Keywords: trade, buy, sell, order, execute, place order.";

    public override ToolParameter[] Parameters =>
    [
        new() { Name = P_ACTION, Description = "The action: \"buy\", \"sell\", \"status\", or \"history\".", IsRequired = true },
        new() { Name = P_ACCOUNT_ID, Description = "The MacroSutra account ID.", IsRequired = true },
        new() { Name = P_SYMBOL, Description = "The stock/crypto symbol (required for buy/sell).", IsRequired = false },
        new() { Name = P_SIDE, Description = "Not used directly — determined by action.", IsRequired = false },
        new() { Name = P_QUANTITY, Description = "Number of shares to trade (required for buy/sell).", IsRequired = false },
        new() { Name = P_ORDER_ID, Description = "The trade ID (required for \"status\").", IsRequired = false }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var action = parameters.GetParameterValueOrDefault(P_ACTION);
        var accountId = parameters.GetParameterValueOrDefault(P_ACCOUNT_ID);
        var symbol = parameters.GetParameter(P_SYMBOL, false)?.Value;
        var quantity = parameters.GetParameter(P_QUANTITY, false)?.Value;
        var orderId = parameters.GetParameter(P_ORDER_ID, false)?.Value;

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Trade: {action} {symbol}",
            ExecutionTask = Task.Run(() => ExecuteAsync(toolContext, action, accountId, symbol, quantity, orderId), cancellation)
        };
    }

    internal static async Task<ToolResult> ExecuteAsync(IToolContext context, string action, string accountId,
        string? symbol, string? quantity, string? orderId)
    {
        try
        {
            return action.ToLowerInvariant() switch
            {
                "buy" => await PlaceOrderAsync(context, accountId, symbol, quantity, TradeSide.Buy),
                "sell" => await PlaceOrderAsync(context, accountId, symbol, quantity, TradeSide.Sell),
                "status" => await GetStatusAsync(context, accountId, orderId),
                "history" => await GetHistoryAsync(context, accountId, symbol),
                _ => new ToolResult { Success = false, ErrorMessage = $"Unknown action: {action}. Use \"buy\", \"sell\", \"status\", or \"history\"." }
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static async Task<ToolResult> PlaceOrderAsync(IToolContext context, string accountId,
        string? symbol, string? quantityStr, TradeSide side)
    {
        if (string.IsNullOrEmpty(symbol))
            return new ToolResult { Success = false, ErrorMessage = "symbol is required for buy/sell." };
        if (string.IsNullOrEmpty(quantityStr) || !decimal.TryParse(quantityStr, out var qty) || qty <= 0)
            return new ToolResult { Success = false, ErrorMessage = "quantity must be a positive number." };

        var portfolioService = context.Services.GetRequiredService<PortfolioService>();
        var tradeService = context.Services.GetRequiredService<TradeService>();

        // Get first active brokerage account
        var accounts = await portfolioService.GetBrokerageAccountsAsync(accountId, activeOnly: true);
        var brokerageAccount = accounts.FirstOrDefault(a => a.Provider != BrokerageProvider.Paper)
            ?? accounts.FirstOrDefault();
        if (brokerageAccount == null)
            return new ToolResult { Success = false, ErrorMessage = "No active brokerage account found. Link one first." };

        var trade = new Trade
        {
            AccountId = accountId,
            UserId = "",
            BrokerageAccountId = brokerageAccount.id,
            Symbol = symbol.ToUpperInvariant(),
            Side = side,
            OrderType = TradeActionType.MarketOrder,
            Quantity = qty,
            Status = TradeStatus.Pending
        };

        trade = await tradeService.RecordTradeAsync(trade);

        var providerFactory = context.Services.GetRequiredService<Brokers.BrokerageProviderFactory>();
        var provider = providerFactory.GetProvider(brokerageAccount.Provider);

        try
        {
            var externalId = await provider.PlaceOrderAsync(brokerageAccount.CredentialData, trade);
            trade.ExternalOrderId = externalId;
            trade.Status = TradeStatus.Submitted;
            await tradeService.UpdateTradeStatusAsync(trade.id, accountId, TradeStatus.Submitted);

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(new { trade.id, trade.Symbol, Side = side.ToString(), trade.Quantity, Status = "Submitted", trade.ExternalOrderId }),
                OutputMessage = $"Order placed: {side} {qty} {symbol} via {brokerageAccount.Provider}.",
                OutputFormat = InferenceOutputFormats.Json
            };
        }
        catch (Exception ex)
        {
            await tradeService.UpdateTradeStatusAsync(trade.id, accountId, TradeStatus.Failed);
            return new ToolResult { Success = false, ErrorMessage = $"Order failed: {ex.Message}" };
        }
    }

    private static async Task<ToolResult> GetStatusAsync(IToolContext context, string accountId, string? orderId)
    {
        if (string.IsNullOrEmpty(orderId))
            return new ToolResult { Success = false, ErrorMessage = "orderId is required for the \"status\" action." };

        var tradeService = context.Services.GetRequiredService<TradeService>();
        var trade = await tradeService.GetTradeAsync(orderId, accountId);
        if (trade == null)
            return new ToolResult { Success = false, ErrorMessage = $"Trade '{orderId}' not found." };

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(new
            {
                trade.id, trade.Symbol, Side = trade.Side.ToString(), trade.Quantity,
                Status = trade.Status.ToString(), trade.FilledPrice, trade.FilledQuantity,
                trade.FilledUtc, trade.Notes
            }),
            OutputMessage = $"Trade {trade.id}: {trade.Status}",
            OutputFormat = InferenceOutputFormats.Json
        };
    }

    private static async Task<ToolResult> GetHistoryAsync(IToolContext context, string accountId, string? symbol)
    {
        var tradeService = context.Services.GetRequiredService<TradeService>();
        var trades = await tradeService.GetTradesAsync(accountId, symbol: symbol);
        var output = trades.Take(50).Select(t => new
        {
            t.id, t.Symbol, Side = t.Side.ToString(), t.Quantity,
            Status = t.Status.ToString(), t.FilledPrice, t.CreatedUtc
        });

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(output),
            OutputMessage = $"Found {trades.Count} trade(s).",
            OutputFormat = InferenceOutputFormats.Json
        };
    }
}
