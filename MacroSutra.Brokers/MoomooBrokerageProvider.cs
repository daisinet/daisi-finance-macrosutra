using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Brokers;

/// <summary>
/// Brokerage provider for moomoo/Futu via OpenAPI SDK.
/// Requires OpenD gateway running on localhost. Paper mode uses simulated environment.
/// </summary>
public class MoomooBrokerageProvider : IBrokerageProvider
{
    private const int DefaultPort = 11111;

    public async Task<bool> ValidateCredentialsAsync(string credentialRef)
    {
        try
        {
            var (creds, isPaper) = ParseCredentials(credentialRef);
            _ = isPaper;
            // In production: connect to OpenD gateway, call GetAccList
            await Task.CompletedTask;
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
        _ = isPaper;

        // In production: connect to OpenD, call GetPositionList with TrdEnv
        await Task.CompletedTask;
        return new List<Position>();
    }

    public async Task<string> PlaceOrderAsync(string credentialRef, Trade trade)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        _ = isPaper;

        // In production: connect to OpenD, unlock trade, call PlaceOrder
        await Task.CompletedTask;
        return "";
    }

    public async Task<Trade> GetOrderStatusAsync(string credentialRef, string externalOrderId)
    {
        var (creds, isPaper) = ParseCredentials(credentialRef);
        _ = isPaper;

        // In production: connect to OpenD, call GetOrderList, filter by orderId
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

        // In production: connect to OpenD, call GetFunds
        await Task.CompletedTask;
        return 0m;
    }

    internal static (MoomooCredentials creds, bool isPaper) ParseCredentials(string credentialRef)
    {
        var doc = JsonDocument.Parse(credentialRef);
        var creds = new MoomooCredentials
        {
            Host = doc.RootElement.TryGetProperty("Host", out var h) ? h.GetString() ?? "127.0.0.1" : "127.0.0.1",
            Port = doc.RootElement.TryGetProperty("Port", out var p) ? p.GetInt32() : DefaultPort,
            TradingPassword = doc.RootElement.TryGetProperty("TradingPassword", out var tp) ? tp.GetString() ?? "" : "",
            SecurityFirm = doc.RootElement.TryGetProperty("SecurityFirm", out var sf) ? sf.GetString() ?? "FutuSecurities" : "FutuSecurities"
        };
        var isPaper = doc.RootElement.TryGetProperty("IsPaperTrading", out var ip) && ip.GetBoolean();
        return (creds, isPaper);
    }

    internal static TradeStatus MapOrderStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "NONE" or "UNSUBMITTED" or "WAITING_SUBMIT" => TradeStatus.Pending,
            "SUBMITTED" or "SUBMITTING" => TradeStatus.Submitted,
            "FILLED_PART" => TradeStatus.PartiallyFilled,
            "FILLED_ALL" => TradeStatus.Filled,
            "CANCELLED_PART" or "CANCELLED_ALL" or "DELETED" => TradeStatus.Cancelled,
            "FAILED" or "DISABLED" => TradeStatus.Failed,
            _ => TradeStatus.Pending
        };
    }
}
