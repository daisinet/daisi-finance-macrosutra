using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for TradeStation using REST via HttpClient.
/// Supports SIM (paper) and live modes with OAuth 2.0 token refresh.
/// </summary>
public class TradeStationBrokerageProvider(IHttpClientFactory httpClientFactory) : IBrokerageProvider
{
    private const string LiveBaseUrl = "https://api.tradestation.com/v3";
    private const string SandboxBaseUrl = "https://sim-api.tradestation.com/v3";

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            using var client = CreateHttpClient(creds, isPaper);
            var response = await client.GetAsync("/brokerage/accounts");
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

        var accountsResp = await client.GetAsync("/brokerage/accounts");
        accountsResp.EnsureSuccessStatusCode();
        var accountsJson = await accountsResp.Content.ReadFromJsonAsync<JsonElement>();

        var positions = new List<Position>();
        if (accountsJson.TryGetProperty("Accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array)
        {
            foreach (var account in accounts.EnumerateArray())
            {
                var accountId = account.TryGetProperty("AccountID", out var aid) ? aid.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(accountId)) continue;

                var posResp = await client.GetAsync($"/brokerage/accounts/{accountId}/positions");
                if (!posResp.IsSuccessStatusCode) continue;
                var posJson = await posResp.Content.ReadFromJsonAsync<JsonElement>();

                if (posJson.TryGetProperty("Positions", out var posArray) && posArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in posArray.EnumerateArray())
                    {
                        positions.Add(new Position
                        {
                            Symbol = p.TryGetProperty("Symbol", out var sym) ? sym.GetString() ?? "" : "",
                            Quantity = p.TryGetProperty("Quantity", out var qty) ? decimal.Parse(qty.GetString() ?? "0") : 0,
                            AverageCost = p.TryGetProperty("AveragePrice", out var cost) ? decimal.Parse(cost.GetString() ?? "0") : 0,
                            CurrentPrice = p.TryGetProperty("Last", out var price) ? decimal.Parse(price.GetString() ?? "0") : null
                        });
                    }
                }
                break; // Use first account
            }
        }

        return positions;
    }

    public async Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        // Get first account
        var accountsResp = await client.GetAsync("/brokerage/accounts");
        accountsResp.EnsureSuccessStatusCode();
        var accountsJson = await accountsResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = "";
        if (accountsJson.TryGetProperty("Accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array
            && accounts.GetArrayLength() > 0)
        {
            accountId = accounts[0].TryGetProperty("AccountID", out var aid) ? aid.GetString() ?? "" : "";
        }

        var orderBody = new
        {
            AccountID = accountId,
            Symbol = trade.Symbol,
            Quantity = ((int)trade.Quantity).ToString(),
            OrderType = MapOrderType(trade.OrderType),
            TradeAction = trade.Side == TradeSide.Buy ? "BUY" : "SELL",
            TimeInForce = new { Duration = "DAY" },
            LimitPrice = trade.LimitPrice?.ToString("F2"),
            StopPrice = trade.StopPrice?.ToString("F2")
        };

        var response = await client.PostAsJsonAsync("/orderexecution/orders", orderBody);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        return result.TryGetProperty("Orders", out var orders) && orders.ValueKind == JsonValueKind.Array
            && orders.GetArrayLength() > 0 && orders[0].TryGetProperty("OrderID", out var oid)
            ? oid.GetString() ?? "" : "";
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var response = await client.GetAsync($"/brokerage/orders/{externalOrderId}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var order = json.TryGetProperty("Orders", out var orders) && orders.ValueKind == JsonValueKind.Array
            && orders.GetArrayLength() > 0 ? orders[0] : json;

        var statusStr = order.TryGetProperty("Status", out var s) ? s.GetString() ?? "" : "";

        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = MapOrderStatus(statusStr),
            FilledPrice = order.TryGetProperty("FilledPrice", out var fp) ? decimal.Parse(fp.GetString() ?? "0") : null,
            FilledQuantity = order.TryGetProperty("FilledQuantity", out var fq) ? decimal.Parse(fq.GetString() ?? "0") : null,
            FilledUtc = order.TryGetProperty("ClosedDateTime", out var cd) && cd.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(cd.GetString(), out var dt) ? dt.ToUniversalTime() : null
                : null
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var accountsResp = await client.GetAsync("/brokerage/accounts");
        accountsResp.EnsureSuccessStatusCode();
        var accountsJson = await accountsResp.Content.ReadFromJsonAsync<JsonElement>();

        if (accountsJson.TryGetProperty("Accounts", out var accounts) && accounts.ValueKind == JsonValueKind.Array
            && accounts.GetArrayLength() > 0)
        {
            var accountId = accounts[0].TryGetProperty("AccountID", out var aid) ? aid.GetString() ?? "" : "";
            var balResp = await client.GetAsync($"/brokerage/accounts/{accountId}/balances");
            if (balResp.IsSuccessStatusCode)
            {
                var balJson = await balResp.Content.ReadFromJsonAsync<JsonElement>();
                if (balJson.TryGetProperty("Balances", out var bals) && bals.ValueKind == JsonValueKind.Array
                    && bals.GetArrayLength() > 0 && bals[0].TryGetProperty("CashBalance", out var cash))
                {
                    return decimal.Parse(cash.GetString() ?? "0");
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

        using var client = httpClientFactory.CreateClient("TradeStation");
        client.BaseAddress = new Uri("https://signin.tradestation.com");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = creds.ClientId,
            ["client_secret"] = creds.ClientSecret,
            ["refresh_token"] = creds.RefreshToken
        });

        var response = await client.PostAsync("/oauth/token", content);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        creds.AccessToken = result.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : creds.AccessToken;
        creds.RefreshToken = result.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : creds.RefreshToken;

        if (result.TryGetProperty("expires_in", out var exp))
            creds.TokenExpiresUtc = DateTime.UtcNow.AddSeconds(exp.GetInt32());

        return JsonSerializer.Serialize(new { creds.ClientId, creds.ClientSecret, creds.AccessToken, creds.RefreshToken, creds.TokenExpiresUtc, IsPaperTrading = isPaper });
    }

    internal static (TradeStationCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new TradeStationCredentials
        {
            ClientId = doc.RootElement.TryGetProperty("ClientId", out var ci) ? ci.GetString() ?? "" : "",
            ClientSecret = doc.RootElement.TryGetProperty("ClientSecret", out var cs) ? cs.GetString() ?? "" : "",
            AccessToken = doc.RootElement.TryGetProperty("AccessToken", out var at) ? at.GetString() ?? "" : "",
            RefreshToken = doc.RootElement.TryGetProperty("RefreshToken", out var rt) ? rt.GetString() ?? "" : "",
            TokenExpiresUtc = doc.RootElement.TryGetProperty("TokenExpiresUtc", out var te) && te.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(te.GetString(), out var dt) ? dt : null
                : null
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var p) && p.GetBoolean();
        return (creds, isPaper);
    }

    private HttpClient CreateHttpClient(TradeStationCredentials creds, bool isPaper)
    {
        var client = httpClientFactory.CreateClient("TradeStation");
        client.BaseAddress = new Uri(isPaper ? SandboxBaseUrl : LiveBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        return client;
    }

    private static string MapOrderType(TradeActionType orderType)
    {
        return orderType switch
        {
            TradeActionType.LimitOrder => "Limit",
            TradeActionType.StopOrder => "StopMarket",
            TradeActionType.StopLimitOrder => "StopLimit",
            _ => "Market"
        };
    }

    internal static TradeStatus MapOrderStatus(string status)
    {
        return status switch
        {
            "ACK" => TradeStatus.Pending,
            "OPN" or "DON" => TradeStatus.Submitted,
            "FPR" => TradeStatus.PartiallyFilled,
            "FLL" => TradeStatus.Filled,
            "CAN" or "OUT" or "EXP" => TradeStatus.Cancelled,
            "REJ" or "UCN" => TradeStatus.Rejected,
            "BRO" => TradeStatus.Failed,
            _ => TradeStatus.Pending
        };
    }
}
