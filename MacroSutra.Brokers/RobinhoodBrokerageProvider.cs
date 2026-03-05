using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for Robinhood crypto trading API.
/// Uses Ed25519 signature authentication. Crypto-only — rejects stock symbols.
/// </summary>
public class RobinhoodBrokerageProvider(IHttpClientFactory httpClientFactory) : IBrokerageProvider
{
    private const string BaseUrl = "https://trading.robinhood.com/api/v1/crypto/trading";

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, _) = ParseCredentials(credentialRef);
            using var client = CreateHttpClient(creds);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var path = "/api/v1/crypto/trading/accounts/";
            var signature = SignRequest(creds, "GET", path, "", timestamp);
            client.DefaultRequestHeaders.Add("x-timestamp", timestamp);
            client.DefaultRequestHeaders.Add("x-signature", signature);
            var response = await client.GetAsync("/accounts/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<Position>> GetPositionsAsync(string credentialRef)
    {
        var (creds, _) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var path = "/api/v1/crypto/trading/holdings/";
        var signature = SignRequest(creds, "GET", path, "", timestamp);
        client.DefaultRequestHeaders.Add("x-timestamp", timestamp);
        client.DefaultRequestHeaders.Add("x-signature", signature);

        var response = await client.GetAsync("/holdings/");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var positions = new List<Position>();

        if (json.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var h in results.EnumerateArray())
            {
                var qty = h.TryGetProperty("quantity_available", out var q) ? q.GetDecimal() : 0;
                if (qty <= 0) continue;

                positions.Add(new Position
                {
                    Symbol = h.TryGetProperty("asset_code", out var sym) ? sym.GetString() ?? "" : "",
                    Quantity = qty,
                    AverageCost = h.TryGetProperty("cost_basis", out var cb) && h.TryGetProperty("quantity_available", out var qa)
                        && qa.GetDecimal() > 0 ? cb.GetDecimal() / qa.GetDecimal() : 0,
                    CurrentPrice = h.TryGetProperty("mark_price", out var mp) ? mp.GetDecimal() : null
                });
            }
        }

        return positions;
    }

    public async Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        // Robinhood crypto API only supports crypto symbols
        if (!IsCryptoSymbol(trade.Symbol))
            throw new NotSupportedException($"Robinhood provider only supports crypto trading. '{trade.Symbol}' is not a supported crypto symbol.");

        var (creds, _) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds);

        var orderBody = JsonSerializer.Serialize(new
        {
            client_order_id = Guid.NewGuid().ToString(),
            side = trade.Side == TradeSide.Buy ? "buy" : "sell",
            type = MapOrderType(trade.OrderType),
            symbol = trade.Symbol,
            market_order_config = trade.OrderType == TradeActionType.MarketOrder
                ? new { asset_quantity = trade.Quantity.ToString("G") }
                : null,
            limit_order_config = trade.OrderType == TradeActionType.LimitOrder && trade.LimitPrice.HasValue
                ? new { asset_quantity = trade.Quantity.ToString("G"), limit_price = trade.LimitPrice.Value.ToString("G") }
                : null
        });

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var path = "/api/v1/crypto/trading/orders/";
        var signature = SignRequest(creds, "POST", path, orderBody, timestamp);
        client.DefaultRequestHeaders.Add("x-timestamp", timestamp);
        client.DefaultRequestHeaders.Add("x-signature", signature);

        var content = new StringContent(orderBody, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/orders/", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        return result.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, _) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var path = $"/api/v1/crypto/trading/orders/{externalOrderId}/";
        var signature = SignRequest(creds, "GET", path, "", timestamp);
        client.DefaultRequestHeaders.Add("x-timestamp", timestamp);
        client.DefaultRequestHeaders.Add("x-signature", signature);

        var response = await client.GetAsync($"/orders/{externalOrderId}/");
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();

        var statusStr = order.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";

        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = MapOrderStatus(statusStr),
            FilledPrice = order.TryGetProperty("average_price", out var fp) ? fp.GetDecimal() : null,
            FilledQuantity = order.TryGetProperty("filled_asset_quantity", out var fq) ? fq.GetDecimal() : null,
            FilledUtc = order.TryGetProperty("updated_at", out var ua) && ua.ValueKind == JsonValueKind.String
                ? DateTime.TryParse(ua.GetString(), out var dt) ? dt.ToUniversalTime() : null
                : null
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, _) = ParseCredentials(credentialRef);
        using var client = CreateHttpClient(creds);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var path = "/api/v1/crypto/trading/accounts/";
        var signature = SignRequest(creds, "GET", path, "", timestamp);
        client.DefaultRequestHeaders.Add("x-timestamp", timestamp);
        client.DefaultRequestHeaders.Add("x-signature", signature);

        var response = await client.GetAsync("/accounts/");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (json.TryGetProperty("buying_power", out var bp))
            return bp.GetDecimal();

        return 0m;
    }

    internal static (RobinhoodCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new RobinhoodCredentials
        {
            ApiKey = doc.RootElement.TryGetProperty("ApiKey", out var ak) ? ak.GetString() ?? "" : "",
            ApiSecret = doc.RootElement.TryGetProperty("ApiSecret", out var asc) ? asc.GetString() ?? "" : "",
            Base64PrivateKey = doc.RootElement.TryGetProperty("Base64PrivateKey", out var pk) ? pk.GetString() ?? "" : ""
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var p) && p.GetBoolean();
        return (creds, isPaper);
    }

    private HttpClient CreateHttpClient(RobinhoodCredentials creds)
    {
        var client = httpClientFactory.CreateClient("Robinhood");
        client.BaseAddress = new Uri(BaseUrl);
        client.DefaultRequestHeaders.Add("x-api-key", creds.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string SignRequest(RobinhoodCredentials creds, string method, string path, string body, string timestamp)
    {
        var message = $"{creds.ApiKey}{timestamp}{path}{method}{body}";
        var privateKeyBytes = Convert.FromBase64String(creds.Base64PrivateKey);
        using var ed25519 = ECDsa.Create();
        ed25519.ImportPkcs8PrivateKey(privateKeyBytes, out _);
        var signatureBytes = ed25519.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA512);
        return Convert.ToBase64String(signatureBytes);
    }

    private static bool IsCryptoSymbol(string symbol)
    {
        var cryptoSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BTC", "ETH", "DOGE", "SOL", "AVAX", "SHIB", "LINK", "XLM",
            "AAVE", "UNI", "LTC", "ETC", "BCH", "COMP", "MATIC", "XTZ",
            "BTC-USD", "ETH-USD", "DOGE-USD", "SOL-USD", "AVAX-USD"
        };
        return cryptoSymbols.Contains(symbol);
    }

    private static string MapOrderType(TradeActionType orderType)
    {
        return orderType switch
        {
            TradeActionType.LimitOrder => "limit",
            _ => "market"
        };
    }

    internal static TradeStatus MapOrderStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" or "queued" or "unconfirmed" => TradeStatus.Pending,
            "confirmed" or "placed" => TradeStatus.Submitted,
            "partially_filled" => TradeStatus.PartiallyFilled,
            "filled" => TradeStatus.Filled,
            "canceled" or "cancelled" => TradeStatus.Cancelled,
            "expired" => TradeStatus.Cancelled,
            "rejected" or "failed" => TradeStatus.Rejected,
            _ => TradeStatus.Pending
        };
    }
}
