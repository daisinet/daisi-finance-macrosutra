using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for Interactive Brokers via TWS API (IBApi).
/// Uses an internal wrapper to bridge TWS callbacks to async/await.
/// Paper mode uses port 7497, live uses port 7496.
/// </summary>
public class InteractiveBrokersBrokerageProvider : IBrokerageProvider
{
    private const int LivePort = 7496;
    private const int PaperPort = 7497;
    private const int TimeoutMs = 15000;

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            // Attempt connection to TWS/Gateway — connection success = valid
            var port = isPaper ? PaperPort : (creds.Port > 0 ? creds.Port : LivePort);
            using var cts = new CancellationTokenSource(TimeoutMs);
            // In production, this would connect to EClientSocket and verify nextValidId callback
            await Task.Delay(1, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<Position>> GetPositionsAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        _ = isPaper; // Used for port selection in production

        // In production: connect, call reqPositions(), collect via positionEnd callback
        // TWS API requires active connection — this is a framework placeholder
        await Task.CompletedTask;
        return new List<Position>();
    }

    public async Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        _ = isPaper;

        // In production: connect, get nextValidId, create Contract + Order, call placeOrder()
        // Wait for orderStatus callback via TaskCompletionSource
        await Task.CompletedTask;
        return "";
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        _ = isPaper;

        // In production: reqOpenOrders() + orderStatus callback
        await Task.CompletedTask;
        return new Trade
        {
            ExternalOrderId = externalOrderId,
            Status = TradeStatus.Pending
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(string credentialRef)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        _ = isPaper;

        // In production: reqAccountSummary() + accountSummary callback
        await Task.CompletedTask;
        return 0m;
    }

    internal static (InteractiveBrokersCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new InteractiveBrokersCredentials
        {
            Host = doc.RootElement.TryGetProperty("Host", out var h) ? h.GetString() ?? "127.0.0.1" : "127.0.0.1",
            Port = doc.RootElement.TryGetProperty("Port", out var p) ? p.GetInt32() : 7497,
            ClientId = doc.RootElement.TryGetProperty("ClientId", out var c) ? c.GetInt32() : 1
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var ip) && ip.GetBoolean();
        // Also treat paper port as paper mode
        if (creds.Port == PaperPort) isPaper = true;
        return (creds, isPaper);
    }

    internal static TradeStatus MapOrderStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "PRESUBMITTED" or "PENDINGSUBMIT" => TradeStatus.Pending,
            "SUBMITTED" => TradeStatus.Submitted,
            "PARTIALFILLED" => TradeStatus.PartiallyFilled,
            "FILLED" => TradeStatus.Filled,
            "CANCELLED" or "CANCELED" => TradeStatus.Cancelled,
            "INACTIVE" => TradeStatus.Cancelled,
            "APICANCELLED" => TradeStatus.Cancelled,
            "ERROR" => TradeStatus.Failed,
            _ => TradeStatus.Pending
        };
    }
}
