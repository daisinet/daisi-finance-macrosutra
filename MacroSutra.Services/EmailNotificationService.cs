using System.Text;
using System.Text.Json;
using MacroSutra.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Sends trade alert emails via SendGrid v3 API.
/// Non-throwing — returns success/failure so callers can record the result.
/// </summary>
public class EmailNotificationService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<EmailNotificationService> logger)
{
    /// <summary>
    /// Sends a trade alert email to the subscriber.
    /// </summary>
    public virtual async Task<bool> SendTradeAlertAsync(
        string toEmail, string subscriberName, Trade trade, TradingStrategy strategy)
    {
        var apiKey = configuration["Email:SendGridApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("SendGrid API key not configured — skipping email notification");
            return false;
        }

        var fromAddress = configuration["Email:FromAddress"] ?? "alerts@macrosutra.com";
        var fromName = configuration["Email:FromName"] ?? "MacroSutra";

        try
        {
            var client = httpClientFactory.CreateClient("SendGrid");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = new[] { new { email = toEmail, name = subscriberName } },
                        subject = $"Trade Alert: {strategy.Name} — {trade.Side} {trade.Symbol}"
                    }
                },
                from = new { email = fromAddress, name = fromName },
                content = new[]
                {
                    new
                    {
                        type = "text/plain",
                        value = $"""
                            Trade Alert from MacroSutra

                            Strategy: {strategy.Name}
                            Symbol: {trade.Symbol}
                            Side: {trade.Side}
                            Quantity: {trade.Quantity}
                            Price: {trade.FilledPrice:C}
                            Time: {trade.CreatedUtc:u}

                            This alert was sent because you are subscribed to this strategy.
                            Manage your subscriptions at https://macrosutra.com/subscriptions
                            """
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.sendgrid.com/v3/mail/send", content);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Email sent to {Email} for trade {TradeId}", toEmail, trade.id);
                return true;
            }

            logger.LogWarning("SendGrid returned {StatusCode} for trade {TradeId}",
                response.StatusCode, trade.id);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Email} for trade {TradeId}", toEmail, trade.id);
            return false;
        }
    }
}
