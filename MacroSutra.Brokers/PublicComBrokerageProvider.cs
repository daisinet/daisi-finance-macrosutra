using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for Public.com using REST via HttpClient.
/// Supports API key authentication and fractional shares.
/// </summary>
public class PublicComBrokerageProvider(IHttpClientFactory httpClientFactory) : IBrokerageProvider
{
    public bool SupportsFractionalShares => true;

    private const string LiveBaseUrl = "https://api.public.com/v1";
    private const string SandboxBaseUrl = "https://sandbox.public.com/v1";

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            using var client = CreateHttpClient(creds, isPaper);
            var response = await client.GetAsync("/account");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<Position>> GetPositionsAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);
        var response = await client.GetAsync("/account/positions");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var positions = new List<Position>();

        if (json.TryGetProperty("positions", out var posArray) && posArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in posArray.EnumerateArray())
            {
                positions.Add(new Position
                {
                    Symbol = p.TryGetProperty("symbol", out var sym) ? sym.GetString() ?? "" : "",
                    Quantity = p.TryGetProperty("quantity", out var qty) ? qty.GetDecimal() : 0,
                    AverageCost = p.TryGetProperty("average_price", out var cost) ? cost.GetDecimal() : 0,
                    CurrentPrice = p.TryGetProperty("current_price", out var price) ? price.GetDecimal() : null
                });
            }
        }

        return positions;
    }

    public async Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var orderBody = new
        {
            symbol = trade.Symbol,
            side = trade.Side == TradeSide.Buy ? "buy" : "sell",
            type = MapOrderType(trade.OrderType),
            quantity = trade.Quantity,
            limit_price = trade.LimitPrice,
            stop_price = trade.StopPrice,
            time_in_force = "day"
        };

        var response = await client.PostAsJsonAsync("/orders", orderBody);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.TryGetProperty("order_id", out var oid) ? oid.GetString() ?? "" : "";
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync($"/orders/{externalOrderId}");
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();

        var statusStr = order.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = MapOrderStatus(statusStr),
            FilledPrice = order.TryGetProperty("filled_avg_price", out var fp) ? fp.GetDecimal() : null,
            FilledQuantity = order.TryGetProperty("filled_quantity", out var fq) ? fq.GetDecimal() : null,
            FilledUtc = order.TryGetProperty("filled_at", out var fa) && fa.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(fa.GetString(), out var dt) ? dt.ToUniversalTime() : null
                : null
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync("/account");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return json.TryGetProperty("cash_balance", out var cash) ? cash.GetDecimal() : 0m;
    }

    internal static (PublicComCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new PublicComCredentials
        {
            ApiKey = doc.RootElement.TryGetProperty("ApiKey", out var ak) ? ak.GetString() ?? "" : "",
            SecretKey = doc.RootElement.TryGetProperty("SecretKey", out var sk) ? sk.GetString() ?? "" : ""
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var p) && p.GetBoolean();
        return (creds, isPaper);
    }

    private HttpClient CreateHttpClient(PublicComCredentials creds, bool isPaper)
    {
        var client = httpClientFactory.CreateClient("PublicCom");
        client.BaseAddress = new Uri(isPaper ? SandboxBaseUrl : LiveBaseUrl);
        client.DefaultRequestHeaders.Add("X-Api-Key", creds.ApiKey);
        client.DefaultRequestHeaders.Add("X-Api-Secret", creds.SecretKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string MapOrderType(TradeActionType orderType)
    {
        return orderType switch
        {
            TradeActionType.LimitOrder => "limit",
            TradeActionType.StopOrder => "stop",
            TradeActionType.StopLimitOrder => "stop_limit",
            _ => "market"
        };
    }

    internal static TradeStatus MapOrderStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" or "new" => TradeStatus.Pending,
            "open" or "accepted" => TradeStatus.Submitted,
            "partially_filled" => TradeStatus.PartiallyFilled,
            "filled" => TradeStatus.Filled,
            "canceled" or "cancelled" => TradeStatus.Cancelled,
            "expired" => TradeStatus.Cancelled,
            "rejected" => TradeStatus.Rejected,
            _ => TradeStatus.Pending
        };
    }
}
