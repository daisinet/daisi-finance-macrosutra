using System.Text.Json;
using Alpaca.Markets;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for Alpaca using the official Alpaca.Markets SDK.
/// Supports both paper and live trading via the IsPaperTrading flag.
/// </summary>
public class AlpacaBrokerageProvider : IBrokerageProvider
{
    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            using var client = CreateClient(creds, isPaper);
            var account = await client.GetAccountAsync();
            return account != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<Position>> GetPositionsAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateClient(creds, isPaper);
        var alpacaPositions = await client.ListPositionsAsync();

        return alpacaPositions.Select(p => new Position
        {
            Symbol = p.Symbol,
            Quantity = p.Quantity,
            AverageCost = p.AverageEntryPrice,
            CurrentPrice = p.AssetCurrentPrice
        }).ToList();
    }

    public async Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateClient(creds, isPaper);

        var side = trade.Side == TradeSide.Buy ? OrderSide.Buy : OrderSide.Sell;

        IOrder order;
        switch (trade.OrderType)
        {
            case TradeActionType.LimitOrder when trade.LimitPrice.HasValue:
                order = await client.PostOrderAsync(
                    side.Limit(trade.Symbol, OrderQuantity.Fractional(trade.Quantity), trade.LimitPrice.Value));
                break;
            case TradeActionType.StopOrder when trade.StopPrice.HasValue:
                order = await client.PostOrderAsync(
                    side.Stop(trade.Symbol, OrderQuantity.Fractional(trade.Quantity), trade.StopPrice.Value));
                break;
            case TradeActionType.StopLimitOrder when trade.StopPrice.HasValue && trade.LimitPrice.HasValue:
                order = await client.PostOrderAsync(
                    side.StopLimit(trade.Symbol, OrderQuantity.Fractional(trade.Quantity), trade.StopPrice.Value, trade.LimitPrice.Value));
                break;
            default:
                order = await client.PostOrderAsync(
                    side.Market(trade.Symbol, OrderQuantity.Fractional(trade.Quantity)));
                break;
        }

        return order.OrderId.ToString();
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateClient(creds, isPaper);

        var order = await client.GetOrderAsync(Guid.Parse(externalOrderId));

        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = MapOrderStatus(order.OrderStatus),
            FilledPrice = order.AverageFillPrice,
            FilledQuantity = order.FilledQuantity,
            FilledUtc = order.FilledAtUtc
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateClient(creds, isPaper);
        var account = await client.GetAccountAsync();
        return account.TradableCash;
    }

    internal static TradeStatus MapOrderStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.New => TradeStatus.Submitted,
            OrderStatus.Accepted => TradeStatus.Submitted,
            OrderStatus.PendingNew => TradeStatus.Pending,
            OrderStatus.AcceptedForBidding => TradeStatus.Submitted,
            OrderStatus.PartiallyFilled => TradeStatus.PartiallyFilled,
            OrderStatus.Filled => TradeStatus.Filled,
            OrderStatus.Canceled => TradeStatus.Cancelled,
            OrderStatus.Expired => TradeStatus.Cancelled,
            OrderStatus.Rejected => TradeStatus.Rejected,
            OrderStatus.PendingCancel => TradeStatus.Submitted,
            OrderStatus.PendingReplace => TradeStatus.Submitted,
            OrderStatus.Replaced => TradeStatus.Submitted,
            OrderStatus.Stopped => TradeStatus.Cancelled,
            OrderStatus.Suspended => TradeStatus.Submitted,
            OrderStatus.Held => TradeStatus.Submitted,
            _ => TradeStatus.Pending
        };
    }

    internal static (AlpacaCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new AlpacaCredentials
        {
            ApiKey = doc.RootElement.GetProperty("ApiKey").GetString() ?? "",
            SecretKey = doc.RootElement.GetProperty("SecretKey").GetString() ?? ""
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var p) && p.GetBoolean();
        return (creds, isPaper);
    }

    private static IAlpacaTradingClient CreateClient(AlpacaCredentials creds, bool isPaper)
    {
        var environment = isPaper ? Environments.Paper : Environments.Live;
        return environment.GetAlpacaTradingClient(new SecretKey(creds.ApiKey, creds.SecretKey));
    }
}
