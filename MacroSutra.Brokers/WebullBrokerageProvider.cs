using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for Webull OpenAPI using REST via HttpClient.
/// Supports paper (sandbox) and live modes, with OAuth token refresh.
/// </summary>
public class WebullBrokerageProvider(IHttpClientFactory httpClientFactory) : IBrokerageProvider
{
    private const string LiveBaseUrl = "https://api.webull.com/api";
    private const string SandboxBaseUrl = "https://sandbox.webull.com/api";

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            using var client = CreateHttpClient(creds, isPaper);
            var response = await client.GetAsync("/account/profile");
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
                    Symbol = p.TryGetProperty("ticker", out var t) ? t.GetProperty("symbol").GetString() ?? "" : "",
                    Quantity = p.TryGetProperty("position", out var qty) ? qty.GetDecimal() : 0,
                    AverageCost = p.TryGetProperty("costPrice", out var cost) ? cost.GetDecimal() : 0,
                    CurrentPrice = p.TryGetProperty("lastPrice", out var price) ? price.GetDecimal() : null
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
            action = trade.Side == TradeSide.Buy ? "BUY" : "SELL",
            orderType = MapOrderType(trade.OrderType),
            quantity = trade.Quantity,
            limitPrice = trade.LimitPrice,
            stopPrice = trade.StopPrice,
            timeInForce = "DAY"
        };

        var response = await client.PostAsJsonAsync("/order/place", orderBody);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("orderId").GetString() ?? "";
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync($"/order/{externalOrderId}");
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();

        var statusStr = order.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = MapOrderStatus(statusStr),
            FilledPrice = order.TryGetProperty("avgFilledPrice", out var fp) ? fp.GetDecimal() : null,
            FilledQuantity = order.TryGetProperty("filledQuantity", out var fq) ? fq.GetDecimal() : null,
            FilledUtc = order.TryGetProperty("filledTime", out var ft) && ft.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(ft.GetString(), out var dt) ? dt.ToUniversalTime() : null
                : null
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync("/account/profile");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return json.TryGetProperty("totalCashValue", out var cash) ? cash.GetDecimal() : 0m;
    }

    public async Task<string?> TryRefreshCredentialsAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);

        if (string.IsNullOrEmpty(creds.RefreshToken))
            return null;

        // Refresh if token expires within 5 minutes
        if (creds.TokenExpiresUtc.HasValue && creds.TokenExpiresUtc.Value > DateTime.UtcNow.AddMinutes(5))
            return null;

        using var client = httpClientFactory.CreateClient("Webull");
        client.BaseAddress = new Uri(isPaper ? SandboxBaseUrl : LiveBaseUrl);

        var refreshBody = new
        {
            grant_type = "refresh_token",
            app_key = creds.AppKey,
            app_secret = creds.AppSecret,
            refresh_token = creds.RefreshToken
        };

        var response = await client.PostAsJsonAsync("/oauth/token", refreshBody);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        creds.AccessToken = result.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : creds.AccessToken;
        creds.RefreshToken = result.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : creds.RefreshToken;

        if (result.TryGetProperty("expires_in", out var exp))
            creds.TokenExpiresUtc = DateTime.UtcNow.AddSeconds(exp.GetInt32());

        return JsonSerializer.Serialize(new { creds.AppKey, creds.AppSecret, creds.AccessToken, creds.RefreshToken, creds.TokenExpiresUtc, IsPaperTrading = isPaper });
    }

    internal static (WebullCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new WebullCredentials
        {
            AppKey = doc.RootElement.TryGetProperty("AppKey", out var ak) ? ak.GetString() ?? "" : "",
            AppSecret = doc.RootElement.TryGetProperty("AppSecret", out var asc) ? asc.GetString() ?? "" : "",
            AccessToken = doc.RootElement.TryGetProperty("AccessToken", out var at) ? at.GetString() ?? "" : "",
            RefreshToken = doc.RootElement.TryGetProperty("RefreshToken", out var rt) ? rt.GetString() ?? "" : "",
            TokenExpiresUtc = doc.RootElement.TryGetProperty("TokenExpiresUtc", out var te) && te.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(te.GetString(), out var dt) ? dt : null
                : null
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var p) && p.GetBoolean();
        return (creds, isPaper);
    }

    private HttpClient CreateHttpClient(WebullCredentials creds, bool isPaper)
    {
        var client = httpClientFactory.CreateClient("Webull");
        client.BaseAddress = new Uri(isPaper ? SandboxBaseUrl : LiveBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        client.DefaultRequestHeaders.Add("x-app-key", creds.AppKey);
        return client;
    }

    private static string MapOrderType(TradeActionType orderType)
    {
        return orderType switch
        {
            TradeActionType.LimitOrder => "LMT",
            TradeActionType.StopOrder => "STP",
            TradeActionType.StopLimitOrder => "STP LMT",
            _ => "MKT"
        };
    }

    internal static TradeStatus MapOrderStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "PENDING" => TradeStatus.Pending,
            "WORKING" => TradeStatus.Submitted,
            "PARTIALLY_FILLED" => TradeStatus.PartiallyFilled,
            "FILLED" => TradeStatus.Filled,
            "CANCELLED" or "CANCELED" => TradeStatus.Cancelled,
            "REJECTED" => TradeStatus.Rejected,
            "FAILED" => TradeStatus.Failed,
            _ => TradeStatus.Pending
        };
    }
}
