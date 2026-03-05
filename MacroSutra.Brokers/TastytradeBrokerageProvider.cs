using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Core.Models.Options;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for Tastytrade using REST via HttpClient.
/// Supports sandbox (cert) and live modes with session token authentication.
/// </summary>
public class TastytradeBrokerageProvider(IHttpClientFactory httpClientFactory) : IBrokerageProvider
{
    public bool SupportsOptions => true;

    private const string LiveBaseUrl = "https://api.tastytrade.com";
    private const string SandboxBaseUrl = "https://api.cert.tastytrade.com";

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            using var client = CreateHttpClient(creds, isPaper);
            var response = await client.GetAsync("/customers/me");
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

        // Get first account
        var accountsResp = await client.GetAsync("/customers/me/accounts");
        accountsResp.EnsureSuccessStatusCode();
        var accountsJson = await accountsResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountNumber = "";
        if (accountsJson.TryGetProperty("data", out var data) && data.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
        {
            var first = items[0];
            if (first.TryGetProperty("account", out var acct) && acct.TryGetProperty("account-number", out var an))
                accountNumber = an.GetString() ?? "";
        }

        var response = await client.GetAsync($"/accounts/{accountNumber}/positions");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var positions = new List<Position>();

        if (json.TryGetProperty("data", out var posData) && posData.TryGetProperty("items", out var posArray)
            && posArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in posArray.EnumerateArray())
            {
                positions.Add(new Position
                {
                    Symbol = p.TryGetProperty("underlying-symbol", out var sym) ? sym.GetString() ?? "" : "",
                    Quantity = p.TryGetProperty("quantity", out var qty) ? qty.GetDecimal() : 0,
                    AverageCost = p.TryGetProperty("average-open-price", out var cost) ? cost.GetDecimal() : 0,
                    CurrentPrice = p.TryGetProperty("close-price", out var price) ? price.GetDecimal() : null
                });
            }
        }

        return positions;
    }

    public async Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        // Get first account number
        var accountsResp = await client.GetAsync("/customers/me/accounts");
        accountsResp.EnsureSuccessStatusCode();
        var accountsJson = await accountsResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountNumber = "";
        if (accountsJson.TryGetProperty("data", out var data) && data.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
        {
            var first = items[0];
            if (first.TryGetProperty("account", out var acct) && acct.TryGetProperty("account-number", out var an))
                accountNumber = an.GetString() ?? "";
        }

        var orderBody = new
        {
            order_type = MapOrderType(trade.OrderType),
            time_in_force = "Day",
            price = trade.LimitPrice,
            stop_trigger = trade.StopPrice,
            legs = new[]
            {
                new
                {
                    instrument_type = "Equity",
                    symbol = trade.Symbol,
                    action = trade.Side == TradeSide.Buy ? "Buy to Open" : "Sell to Close",
                    quantity = (int)trade.Quantity
                }
            }
        };

        var response = await client.PostAsJsonAsync($"/accounts/{accountNumber}/orders", orderBody);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        return result.TryGetProperty("data", out var d) && d.TryGetProperty("order", out var order)
            && order.TryGetProperty("id", out var id) ? id.ToString() : "";
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        // Get first account number
        var accountsResp = await client.GetAsync("/customers/me/accounts");
        accountsResp.EnsureSuccessStatusCode();
        var accountsJson = await accountsResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountNumber = "";
        if (accountsJson.TryGetProperty("data", out var accData) && accData.TryGetProperty("items", out var accItems)
            && accItems.ValueKind == JsonValueKind.Array && accItems.GetArrayLength() > 0)
        {
            var first = accItems[0];
            if (first.TryGetProperty("account", out var acct) && acct.TryGetProperty("account-number", out var an))
                accountNumber = an.GetString() ?? "";
        }

        var response = await client.GetAsync($"/accounts/{accountNumber}/orders/{externalOrderId}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        var order = json.TryGetProperty("data", out var d) ? d : json;
        var statusStr = order.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = MapOrderStatus(statusStr),
            FilledPrice = order.TryGetProperty("price-effect-description", out _)
                && order.TryGetProperty("average-fill-price", out var fp) ? fp.GetDecimal() : null,
            FilledQuantity = order.TryGetProperty("filled-quantity", out var fq) ? fq.GetDecimal() : null,
            FilledUtc = order.TryGetProperty("updated-at", out var ua) && ua.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(ua.GetString(), out var dt) ? dt.ToUniversalTime() : null
                : null
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var accountsResp = await client.GetAsync("/customers/me/accounts");
        accountsResp.EnsureSuccessStatusCode();
        var accountsJson = await accountsResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountNumber = "";
        if (accountsJson.TryGetProperty("data", out var data) && data.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
        {
            var first = items[0];
            if (first.TryGetProperty("account", out var acct) && acct.TryGetProperty("account-number", out var an))
                accountNumber = an.GetString() ?? "";
        }

        var response = await client.GetAsync($"/accounts/{accountNumber}/balances");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (json.TryGetProperty("data", out var balData) && balData.TryGetProperty("cash-balance", out var cash))
            return cash.GetDecimal();

        return 0m;
    }

    public async Task<string?> TryRefreshCredentialsAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);

        if (string.IsNullOrEmpty(creds.Username) || string.IsNullOrEmpty(creds.Password))
            return null;

        if (creds.TokenExpiresUtc.HasValue && creds.TokenExpiresUtc.Value > DateTime.UtcNow.AddMinutes(5))
            return null;

        using var client = httpClientFactory.CreateClient("Tastytrade");
        client.BaseAddress = new Uri(isPaper ? SandboxBaseUrl : LiveBaseUrl);

        var loginBody = new { login = creds.Username, password = creds.Password };
        var response = await client.PostAsJsonAsync("/sessions", loginBody);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (result.TryGetProperty("data", out var data) && data.TryGetProperty("session-token", out var token))
        {
            creds.SessionToken = token.GetString() ?? "";
            creds.TokenExpiresUtc = DateTime.UtcNow.AddHours(24);
        }

        return JsonSerializer.Serialize(new { creds.Username, creds.Password, creds.SessionToken, creds.TokenExpiresUtc, IsPaperTrading = isPaper });
    }

    public async Task<OptionsChain> GetOptionsChainAsync(string credentialRef, string underlyingSymbol, DateOnly? expiration = null)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        var chain = new OptionsChain { UnderlyingSymbol = underlyingSymbol };

        try
        {
            var url = $"/option-chains/{underlyingSymbol}/nested";
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                // Parse Tastytrade-specific chain format
                // Full implementation requires Tastytrade API documentation
            }
        }
        catch { }

        return chain;
    }

    public async Task<string> PlaceOptionsOrderAsync(string credentialRef, Trade trade)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds, isPaper);

        // Get first account number
        var accountsResp = await client.GetAsync("/customers/me/accounts");
        accountsResp.EnsureSuccessStatusCode();
        var accountsJson = await accountsResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountNumber = "";
        if (accountsJson.TryGetProperty("data", out var accData) && accData.TryGetProperty("items", out var accItems)
            && accItems.ValueKind == JsonValueKind.Array && accItems.GetArrayLength() > 0)
        {
            var first = accItems[0];
            if (first.TryGetProperty("account", out var acct) && acct.TryGetProperty("account-number", out var an))
                accountNumber = an.GetString() ?? "";
        }

        var contractSymbol = trade.OptionDetails?.ContractSymbol ?? trade.Symbol;
        var action = trade.Side == TradeSide.Buy ? "Buy to Open" : "Sell to Close";

        var orderBody = new
        {
            time_in_force = "Day",
            order_type = trade.LimitPrice.HasValue ? "Limit" : "Market",
            price = trade.LimitPrice,
            legs = new[]
            {
                new
                {
                    instrument_type = "Equity Option",
                    symbol = contractSymbol,
                    quantity = trade.OptionDetails?.Contracts ?? (int)trade.Quantity,
                    action
                }
            }
        };

        var response = await client.PostAsJsonAsync($"/accounts/{accountNumber}/orders", orderBody);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.TryGetProperty("data", out var d) && d.TryGetProperty("order", out var order) && order.TryGetProperty("id", out var id)
            ? id.ToString() : "";
    }

    internal static (TastytradeCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new TastytradeCredentials
        {
            Username = doc.RootElement.TryGetProperty("Username", out var un) ? un.GetString() ?? "" : "",
            Password = doc.RootElement.TryGetProperty("Password", out var pw) ? pw.GetString() ?? "" : "",
            SessionToken = doc.RootElement.TryGetProperty("SessionToken", out var st) ? st.GetString() ?? "" : "",
            TokenExpiresUtc = doc.RootElement.TryGetProperty("TokenExpiresUtc", out var te) && te.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(te.GetString(), out var dt) ? dt : null
                : null
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var p) && p.GetBoolean();
        return (creds, isPaper);
    }

    private HttpClient CreateHttpClient(TastytradeCredentials creds, bool isPaper)
    {
        var client = httpClientFactory.CreateClient("Tastytrade");
        client.BaseAddress = new Uri(isPaper ? SandboxBaseUrl : LiveBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", creds.SessionToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string MapOrderType(TradeActionType orderType)
    {
        return orderType switch
        {
            TradeActionType.LimitOrder => "Limit",
            TradeActionType.StopOrder => "Stop",
            TradeActionType.StopLimitOrder => "Stop Limit",
            _ => "Market"
        };
    }

    internal static TradeStatus MapOrderStatus(string status)
    {
        return status switch
        {
            "Received" => TradeStatus.Pending,
            "Routed" => TradeStatus.Submitted,
            "In Flight" => TradeStatus.Submitted,
            "Live" => TradeStatus.Submitted,
            "Partially Filled" => TradeStatus.PartiallyFilled,
            "Filled" => TradeStatus.Filled,
            "Cancelled" or "Canceled" => TradeStatus.Cancelled,
            "Expired" => TradeStatus.Cancelled,
            "Rejected" => TradeStatus.Rejected,
            _ => TradeStatus.Pending
        };
    }
}
