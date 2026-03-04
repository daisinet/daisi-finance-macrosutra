using System.Net.Http.Json;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Sends push notifications via Firebase Cloud Messaging (FCM).
/// Falls back gracefully when Firebase is not configured.
/// </summary>
public class PushNotificationService(
    MacroSutraCosmo cosmo,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PushNotificationService> logger)
{
    private readonly string? _fcmServerKey = configuration["Firebase:ServerKey"];
    private const string FcmUrl = "https://fcm.googleapis.com/fcm/send";

    /// <summary>
    /// Sends a push notification to all registered devices for a subscriber account.
    /// </summary>
    public virtual async Task<bool> SendTradeAlertAsync(string subscriberAccountId, Trade trade, TradingStrategy strategy)
    {
        if (string.IsNullOrEmpty(_fcmServerKey))
        {
            logger.LogDebug("Firebase not configured — skipping push notification");
            return false;
        }

        var tokens = await cosmo.GetPushTokensAsync(subscriberAccountId);
        if (tokens.Count == 0) return false;

        var sent = false;
        foreach (var token in tokens)
        {
            try
            {
                await SendFcmAsync(new
                {
                    to = token.Token,
                    notification = new
                    {
                        title = $"Strategy Triggered: {strategy.Name}",
                        body = $"{trade.Side} {trade.Quantity} {trade.Symbol}",
                        click_action = "OPEN_TRADES"
                    },
                    data = new
                    {
                        strategyId = strategy.id,
                        tradeId = trade.id,
                        symbol = trade.Symbol
                    }
                });
                token.LastUsedUtc = DateTime.UtcNow;
                await cosmo.UpdatePushTokenAsync(token);
                sent = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send push to token {TokenId}", token.id);
            }
        }

        return sent;
    }

    private async Task SendFcmAsync(object payload)
    {
        using var client = httpClientFactory.CreateClient("FCM");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"key={_fcmServerKey}");

        var response = await client.PostAsJsonAsync(FcmUrl, payload);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Registers a push token for a user's device.
    /// </summary>
    public virtual async Task<PushToken> RegisterTokenAsync(PushToken pushToken)
    {
        return await cosmo.CreatePushTokenAsync(pushToken);
    }

    /// <summary>
    /// Removes a push token.
    /// </summary>
    public virtual async Task RemoveTokenAsync(string id, string accountId)
    {
        await cosmo.DeletePushTokenAsync(id, accountId);
    }
}
