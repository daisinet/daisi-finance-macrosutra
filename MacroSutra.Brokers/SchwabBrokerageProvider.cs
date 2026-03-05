using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for Charles Schwab using REST via HttpClient.
/// Supports paper (sandbox) and live modes, with OAuth 2.0 token refresh.
/// </summary>
public class SchwabBrokerageProvider(IHttpClientFactory httpClientFactory) : IBrokerageProvider
{
    private const string LiveBaseUrl = "https://api.schwabapi.com/trader/v1";
    private const string SandboxBaseUrl = "https://api.schwabapi.com/trader/v1";

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            using var client = CreateHttpClient(creds, isPaper);
            var response = await client.GetAsync("/accounts/accountNumbers");
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
        var response = await client.GetAsync("/accounts?fields=positions");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var positions = new List<Position>();

        if (json.ValueKind == JsonValueKind.Array)
        {
            foreach (var account in json.EnumerateArray())
            {
                if (!account.TryGetProperty("securitiesAccount", out var sa)) continue;
                if (!sa.TryGetProperty("positions", out var posArray) || posArray.ValueKind != JsonValueKind.Array) continue;

                foreach (var p in posArray.EnumerateArray())
                {
                    positions.Add(new Position
                    {
                        Symbol = p.TryGetProperty("instrument", out var inst) && inst.TryGetProperty("symbol", out var sym)
                            ? sym.GetString() ?? "" : "",
                        Quantity = p.TryGetProperty("longQuantity", out var qty) ? qty.GetDecimal() : 0,
                        AverageCost = p.TryGetProperty("averagePrice", out var cost) ? cost.GetDecimal() : 0,
                        CurrentPrice = p.TryGetProperty("marketValue", out var mv) && p.TryGetProperty("longQuantity", out var q2) && q2.GetDecimal() > 0
                            ? mv.GetDecimal() / q2.GetDecimal() : null
                    });
                }
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
            orderType = MapOrderType(trade.OrderType),
            session = "NORMAL",
            duration = "DAY",
            orderStrategyType = "SINGLE",
            orderLegCollection = new[]
            {
                new
                {
                    instruction = trade.Side == TradeSide.Buy ? "BUY" : "SELL",
                    quantity = trade.Quantity,
                    instrument = new { symbol = trade.Symbol, assetType = "EQUITY" }
                }
            },
            price = trade.LimitPrice,
            stopPrice = trade.StopPrice
        };

        var response = await client.PostAsJsonAsync("/accounts/default/orders", orderBody);
        response.EnsureSuccessStatusCode();

        // Schwab returns order ID in Location header
        var location = response.Headers.Location?.ToString() ?? "";
        var orderId = location.Contains('/') ? location.Split('/').Last() : location;
        return orderId;
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync($"/accounts/default/orders/{externalOrderId}");
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();

        var statusStr = order.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = MapOrderStatus(statusStr),
            FilledPrice = order.TryGetProperty("filledQuantity", out var fq) && fq.GetDecimal() > 0
                && order.TryGetProperty("orderActivityCollection", out var acts) && acts.ValueKind == JsonValueKind.Array
                ? acts.EnumerateArray().LastOrDefault().TryGetProperty("executionLegs", out var legs)
                    && legs.ValueKind == JsonValueKind.Array
                    ? legs.EnumerateArray().LastOrDefault().TryGetProperty("price", out var ep) ? ep.GetDecimal() : null
                    : null
                : null,
            FilledQuantity = order.TryGetProperty("filledQuantity", out var filled) ? filled.GetDecimal() : null,
            FilledUtc = order.TryGetProperty("closeTime", out var ct) && ct.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(ct.GetString(), out var dt) ? dt.ToUniversalTime() : null
                : null
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync("/accounts");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (json.ValueKind == JsonValueKind.Array)
        {
            foreach (var account in json.EnumerateArray())
            {
                if (account.TryGetProperty("securitiesAccount", out var sa)
                    && sa.TryGetProperty("currentBalances", out var bal)
                    && bal.TryGetProperty("cashBalance", out var cash))
                {
                    return cash.GetDecimal();
                }
            }
        }

        return 0m;
    }

    public async Task<string?> TryRefreshCredentialsAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);

        if (string.IsNullOrEmpty(creds.RefreshToken))
            return null;

        if (creds.TokenExpiresUtc.HasValue && creds.TokenExpiresUtc.Value > DateTime.UtcNow.AddMinutes(5))
            return null;

        using var client = httpClientFactory.CreateClient("Schwab");
        client.BaseAddress = new Uri("https://api.schwabapi.com");

        var authHeader = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{creds.AppKey}:{creds.AppSecret}"));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = creds.RefreshToken
        });

        var response = await client.PostAsync("/v1/oauth/token", content);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        creds.AccessToken = result.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : creds.AccessToken;
        creds.RefreshToken = result.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : creds.RefreshToken;

        if (result.TryGetProperty("expires_in", out var exp))
            creds.TokenExpiresUtc = DateTime.UtcNow.AddSeconds(exp.GetInt32());

        return JsonSerializer.Serialize(new { creds.AppKey, creds.AppSecret, creds.AccessToken, creds.RefreshToken, creds.TokenExpiresUtc, IsPaperTrading = isPaper });
    }

    internal static (SchwabCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new SchwabCredentials
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

    private HttpClient CreateHttpClient(SchwabCredentials creds, bool isPaper)
    {
        var client = httpClientFactory.CreateClient("Schwab");
        client.BaseAddress = new Uri(isPaper ? SandboxBaseUrl : LiveBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        return client;
    }

    private static string MapOrderType(TradeActionType orderType)
    {
        return orderType switch
        {
            TradeActionType.LimitOrder => "LIMIT",
            TradeActionType.StopOrder => "STOP",
            TradeActionType.StopLimitOrder => "STOP_LIMIT",
            _ => "MARKET"
        };
    }

    internal static TradeStatus MapOrderStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "PENDING_ACTIVATION" => TradeStatus.Pending,
            "QUEUED" => TradeStatus.Pending,
            "AWAITING_PARENT_ORDER" => TradeStatus.Pending,
            "WORKING" => TradeStatus.Submitted,
            "ACCEPTED" => TradeStatus.Submitted,
            "PENDING_REPLACE" => TradeStatus.Submitted,
            "PENDING_CANCEL" => TradeStatus.Submitted,
            "PARTIALLY_FILLED" => TradeStatus.PartiallyFilled,
            "FILLED" => TradeStatus.Filled,
            "CANCELED" or "CANCELLED" => TradeStatus.Cancelled,
            "EXPIRED" => TradeStatus.Cancelled,
            "REJECTED" => TradeStatus.Rejected,
            _ => TradeStatus.Pending
        };
    }
}
