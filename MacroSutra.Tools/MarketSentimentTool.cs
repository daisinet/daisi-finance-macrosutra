using System.Text;
using System.Text.Json;
using Daisi.Protos.V1;
using Daisi.SDK.Interfaces.Tools;
using Daisi.SDK.Models.Tools;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tools;

/// <summary>
/// AI-powered bot tool that analyzes market sentiment for a symbol.
/// Combines market data with inference to provide sentiment analysis and trading signals.
/// </summary>
public class MarketSentimentTool : DaisiToolBase
{
    private const string P_SYMBOL = "symbol";

    public override string Id => "macrosutra-market-sentiment";
    public override string Name => "MacroSutra Market Sentiment";

    public override string UseInstructions =>
        "Use this tool to get an AI-powered market sentiment analysis for a stock or crypto symbol. " +
        "Analyzes price action, volume, technical indicators, and recent trends. " +
        "Returns a sentiment rating, key observations, and potential trading signals. " +
        "Keywords: sentiment, market analysis, bullish, bearish, outlook, trend, analysis.";

    public override ToolParameter[] Parameters =>
    [
        new() { Name = P_SYMBOL, Description = "The stock/crypto symbol to analyze (e.g. \"AAPL\").", IsRequired = true }
    ];

    public override ToolExecutionContext GetExecutionContext(IToolContext toolContext, CancellationToken cancellation, params ToolParameterBase[] parameters)
    {
        var symbol = parameters.GetParameterValueOrDefault(P_SYMBOL);

        return new ToolExecutionContext
        {
            ExecutionMessage = $"Analyzing sentiment for {symbol}...",
            ExecutionTask = Task.Run(() => ExecuteAsync(toolContext, symbol), cancellation)
        };
    }

    internal static async Task<ToolResult> ExecuteAsync(IToolContext context, string symbol)
    {
        try
        {
            symbol = symbol.ToUpperInvariant();
            var marketDataService = context.Services.GetRequiredService<MarketDataService>();

            // Gather market data
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine($"Symbol: {symbol}");

            var snapshot = await marketDataService.GetSnapshotAsync(symbol);
            if (snapshot != null)
            {
                contextBuilder.AppendLine($"Current Price: ${snapshot.Price:F2}");
                contextBuilder.AppendLine($"Daily Change: {snapshot.DailyChangePercent:F2}%");
                contextBuilder.AppendLine($"Daily Range: ${snapshot.DailyLow:F2} - ${snapshot.DailyHigh:F2}");
                contextBuilder.AppendLine($"Previous Close: ${snapshot.PreviousClose:F2}");
                contextBuilder.AppendLine($"Volume: {snapshot.Volume:N0}");
            }

            // Historical price action (last 50 days)
            var prices = await marketDataService.GetHistoricalPricesAsync(symbol, 50);
            if (prices.Length > 0)
            {
                contextBuilder.AppendLine($"\nHistorical Close Prices (last {prices.Length} trading days, oldest to newest):");
                contextBuilder.AppendLine(string.Join(", ", prices.Select(p => $"${p:F2}")));

                // Basic stats
                var avg = prices.Average();
                var min = prices.Min();
                var max = prices.Max();
                var recent5 = prices.TakeLast(5).Average();
                var recent20 = prices.TakeLast(Math.Min(20, prices.Length)).Average();

                contextBuilder.AppendLine($"\n50-day Average: ${avg:F2}");
                contextBuilder.AppendLine($"50-day Range: ${min:F2} - ${max:F2}");
                contextBuilder.AppendLine($"5-day Average: ${recent5:F2}");
                contextBuilder.AppendLine($"20-day Average: ${recent20:F2}");

                // Price vs moving averages
                var current = prices.Last();
                contextBuilder.AppendLine($"Price vs 20-day MA: {(current > recent20 ? "Above" : "Below")} ({(current - recent20) / recent20 * 100:F1}%)");
                contextBuilder.AppendLine($"Price vs 50-day MA: {(current > avg ? "Above" : "Below")} ({(current - avg) / avg * 100:F1}%)");

                // Simple RSI approximation
                if (prices.Length >= 15)
                {
                    var gains = 0m;
                    var losses = 0m;
                    for (int i = prices.Length - 14; i < prices.Length; i++)
                    {
                        var change = prices[i] - prices[i - 1];
                        if (change > 0) gains += change;
                        else losses -= change;
                    }
                    var avgGain = gains / 14;
                    var avgLoss = losses / 14;
                    var rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
                    var rsi = 100 - (100 / (1 + rs));
                    contextBuilder.AppendLine($"14-day RSI (approx): {rsi:F1}");
                }
            }

            var prompt = BuildPrompt(contextBuilder.ToString());
            var infRequest = SendInferenceRequest.CreateDefault();
            infRequest.Text = prompt;

            var infResult = await context.InferAsync(infRequest);

            return new ToolResult
            {
                Success = true,
                Output = infResult.Content,
                OutputMessage = $"Sentiment analysis for {symbol} complete.",
                OutputFormat = InferenceOutputFormats.Markdown
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    internal static string BuildPrompt(string marketContext)
    {
        return $"""
            You are a market analyst. Analyze the following market data and provide a sentiment assessment.

            {marketContext}

            Provide your analysis in this format:

            ## Sentiment
            Rate as: Strong Bearish / Bearish / Neutral / Bullish / Strong Bullish

            ## Key Observations
            List 3-5 notable data points from the price action, volume, and technical levels.

            ## Technical Outlook
            Brief assessment of support/resistance levels and trend direction.

            ## Trading Signals
            List any actionable signals (e.g. RSI oversold, golden cross approaching, breakout level).

            IMPORTANT: This is analysis only, not financial advice. Be objective and data-driven.
            """;
    }
}
