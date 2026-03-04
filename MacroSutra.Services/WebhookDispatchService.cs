using System.Text;
using System.Text.Json;
using MacroSutra.Core.Models;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// POSTs trade data to a subscriber-configured webhook URL.
/// </summary>
public class WebhookDispatchService(
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookDispatchService> logger)
{
    /// <summary>
    /// Dispatches a trade event to the webhook URL.
    /// Returns success status and HTTP status code (if any).
    /// </summary>
    public virtual async Task<(bool Success, int? StatusCode)> DispatchAsync(
        string webhookUrl, Trade trade, TradingStrategy strategy)
    {
        try
        {
            var payload = new
            {
                @event = "trade_executed",
                strategyId = strategy.id,
                strategyName = strategy.Name,
                symbol = trade.Symbol,
                side = trade.Side.ToString(),
                quantity = trade.Quantity,
                price = trade.FilledPrice,
                executedUtc = trade.CreatedUtc.ToString("o")
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = httpClientFactory.CreateClient("Webhook");
            var response = await client.PostAsync(webhookUrl, content);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Webhook dispatched to {Url} — {StatusCode}", webhookUrl, statusCode);
                return (true, statusCode);
            }

            logger.LogWarning("Webhook to {Url} returned {StatusCode}", webhookUrl, statusCode);
            return (false, statusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook dispatch failed for {Url}", webhookUrl);
            return (false, null);
        }
    }
}
