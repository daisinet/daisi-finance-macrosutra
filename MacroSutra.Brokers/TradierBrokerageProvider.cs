using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for Tradier using REST via HttpClient.
/// Supports sandbox and live modes with bearer token authentication.
/// </summary>
public class TradierBrokerageProvider(IHttpClientFactory httpClientFactory) : IBrokerageProvider
{
    private const string LiveBaseUrl = "https://api.tradier.com/v1";
    private const string SandboxBaseUrl = "https://sandbox.tradier.com/v1";

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            using var client = CreateHttpClient(creds, isPaper);
            var response = await client.GetAsync("/user/profile");
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
        var response = await client.GetAsync($"/accounts/{creds.AccountNumber}/positions");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var positions = new List<Position>();

        if (json.TryGetProperty("positions", out var posObj) && posObj.TryGetProperty("position", out var posArray))
        {
            var items = posArray.ValueKind == JsonValueKind.Array
                ? posArray.EnumerateArray().ToList()
                : new List<JsonElement> { posArray };

            foreach (var p in items)
            {
                positions.Add(new Position
                {
                    Symbol = p.TryGetProperty("symbol", out var sym) ? sym.GetString() ?? "" : "",
                    Quantity = p.TryGetProperty("quantity", out var qty) ? qty.GetDecimal() : 0,
                    AverageCost = p.TryGetProperty("cost_basis", out var cb) && p.TryGetProperty("quantity", out var q2) && q2.GetDecimal() != 0
                        ? cb.GetDecimal() / q2.GetDecimal() : 0,
                    CurrentPrice = p.TryGetProperty("last_price", out var price) ? price.GetDecimal() : null
                });
            }
        }

        return positions;
    }

    public async Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var formData = new Dictionary<string, string>
        {
            ["class"] = "equity",
            ["symbol"] = trade.Symbol,
            ["side"] = trade.Side == TradeSide.Buy ? "buy" : "sell",
            ["quantity"] = ((int)trade.Quantity).ToString(),
            ["type"] = MapOrderType(trade.OrderType),
            ["duration"] = "day"
        };

        if (trade.LimitPrice.HasValue)
            formData["price"] = trade.LimitPrice.Value.ToString("F2");
        if (trade.StopPrice.HasValue)
            formData["stop"] = trade.StopPrice.Value.ToString("F2");

        var content = new FormUrlEncodedContent(formData);
        var response = await client.PostAsync($"/accounts/{creds.AccountNumber}/orders", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        return result.TryGetProperty("order", out var order) && order.TryGetProperty("id", out var id)
            ? id.ToString() : "";
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync($"/accounts/{creds.AccountNumber}/orders/{externalOrderId}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var order = json.TryGetProperty("order", out var o) ? o : json;
        var statusStr = order.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = MapOrderStatus(statusStr),
            FilledPrice = order.TryGetProperty("avg_fill_price", out var fp) ? fp.GetDecimal() : null,
            FilledQuantity = order.TryGetProperty("exec_quantity", out var fq) ? fq.GetDecimal() : null,
            FilledUtc = order.TryGetProperty("transaction_date", out var td) && td.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(td.GetString(), out var dt) ? dt.ToUniversalTime() : null
                : null
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync($"/accounts/{creds.AccountNumber}/balances");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (json.TryGetProperty("balances", out var bal) && bal.TryGetProperty("total_cash", out var cash))
            return cash.GetDecimal();

        return 0m;
    }

    internal static (TradierCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new TradierCredentials
        {
            AccessToken = doc.RootElement.TryGetProperty("AccessToken", out var at) ? at.GetString() ?? "" : "",
            AccountNumber = doc.RootElement.TryGetProperty("AccountNumber", out var an) ? an.GetString() ?? "" : ""
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var p) && p.GetBoolean();
        return (creds, isPaper);
    }

    private HttpClient CreateHttpClient(TradierCredentials creds, bool isPaper)
    {
        var client = httpClientFactory.CreateClient("Tradier");
        client.BaseAddress = new Uri(isPaper ? SandboxBaseUrl : LiveBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
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
            "pending" => TradeStatus.Pending,
            "open" => TradeStatus.Submitted,
            "partially_filled" => TradeStatus.PartiallyFilled,
            "filled" => TradeStatus.Filled,
            "canceled" or "cancelled" => TradeStatus.Cancelled,
            "expired" => TradeStatus.Cancelled,
            "rejected" => TradeStatus.Rejected,
            _ => TradeStatus.Pending
        };
    }
}
