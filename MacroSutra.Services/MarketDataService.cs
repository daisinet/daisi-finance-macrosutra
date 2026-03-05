using System.Collections.Concurrent;
using Alpaca.Markets;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Fetches market data snapshots and historical bars via Alpaca's free data API.
/// Singleton with 30-second in-memory cache to avoid redundant API calls.
/// </summary>
public class MarketDataService
{
    private readonly IAlpacaDataClient _dataClient;
    private readonly ILogger<MarketDataService> _logger;
    private readonly MarketDataFeed? _feed;
    private readonly ConcurrentDictionary<string, (MarketSnapshot snapshot, DateTime fetchedUtc)> _snapshotCache = new();
    private readonly ConcurrentDictionary<string, (decimal[] prices, DateTime fetchedUtc)> _historyCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public MarketDataService(IConfiguration configuration, ILogger<MarketDataService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Alpaca:DataApiKey"] ?? configuration["Alpaca:ApiKey"] ?? "";
        var secretKey = configuration["Alpaca:DataSecretKey"] ?? configuration["Alpaca:SecretKey"] ?? "";

        // Set to "Sip" in user secrets for real-time data (requires paid plan), defaults to IEX (free)
        var feedConfig = configuration["Alpaca:DataFeed"];
        _feed = Enum.TryParse<MarketDataFeed>(feedConfig, true, out var parsed) ? parsed : MarketDataFeed.Iex;

        _dataClient = Environments.Live
            .GetAlpacaDataClient(new SecretKey(apiKey, secretKey));
    }

    /// <summary>
    /// Gets a market snapshot for a symbol (cached for 30s).
    /// </summary>
    public virtual async Task<MarketSnapshot?> GetSnapshotAsync(string symbol)
    {
        if (_snapshotCache.TryGetValue(symbol, out var cached) && DateTime.UtcNow - cached.fetchedUtc < CacheTtl)
            return cached.snapshot;

        try
        {
            var snapshotRequest = new LatestMarketDataRequest(symbol);
            if (_feed.HasValue) snapshotRequest.Feed = _feed.Value;
            var snapshot = await _dataClient.GetSnapshotAsync(snapshotRequest);

            var result = new MarketSnapshot
            {
                Symbol = symbol,
                Price = snapshot.Trade?.Price ?? 0,
                Volume = (long)(snapshot.CurrentDailyBar?.Volume ?? 0),
                DailyHigh = snapshot.CurrentDailyBar?.High ?? 0,
                DailyLow = snapshot.CurrentDailyBar?.Low ?? 0,
                PreviousClose = snapshot.PreviousDailyBar?.Close ?? 0,
                Timestamp = DateTime.UtcNow
            };

            // Calculate daily change %
            if (result.PreviousClose > 0 && result.Price > 0)
                result.DailyChangePercent = (result.Price - result.PreviousClose) / result.PreviousClose * 100;

            _snapshotCache[symbol] = (result, DateTime.UtcNow);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch snapshot for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>
    /// Gets historical daily OHLCV bars for a symbol over a date range (no caching).
    /// </summary>
    public virtual async Task<List<OhlcvBar>> GetHistoricalBarsAsync(string symbol, DateOnly from, DateOnly to) =>
        await GetHistoricalBarsAsync(symbol, from, to, Core.Enums.BarTimeFrame.Day);

    /// <summary>
    /// Gets historical OHLCV bars for a symbol over a date range with specified time frame (no caching).
    /// </summary>
    public virtual async Task<List<OhlcvBar>> GetHistoricalBarsAsync(string symbol, DateOnly from, DateOnly to, Core.Enums.BarTimeFrame timeFrame)
    {
        try
        {
            var alpacaTimeFrame = MapTimeFrame(timeFrame);
            // IEX (free) has ~15-min delay; only SIP can query up to now
            var delay = _feed == MarketDataFeed.Sip ? TimeSpan.Zero : TimeSpan.FromMinutes(16);
            var latest = DateTime.UtcNow - delay;
            var end = to >= DateOnly.FromDateTime(DateTime.UtcNow)
                ? latest
                : to.ToDateTime(TimeOnly.MaxValue);
            var request = new HistoricalBarsRequest(symbol,
                from.ToDateTime(TimeOnly.MinValue),
                end,
                alpacaTimeFrame);
            if (_feed.HasValue) request.Feed = _feed.Value;

            var bars = await _dataClient.ListHistoricalBarsAsync(request);
            return bars.Items.Select(b => new OhlcvBar(
                DateOnly.FromDateTime(b.TimeUtc),
                b.Open,
                b.High,
                b.Low,
                b.Close,
                (long)b.Volume
            )
            { Timestamp = b.TimeUtc }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch historical bars for {Symbol} from {From} to {To} ({TimeFrame})", symbol, from, to, timeFrame);
            return [];
        }
    }

    private static Alpaca.Markets.BarTimeFrame MapTimeFrame(Core.Enums.BarTimeFrame tf) => tf switch
    {
        Core.Enums.BarTimeFrame.OneMinute => Alpaca.Markets.BarTimeFrame.Minute,
        Core.Enums.BarTimeFrame.FiveMinutes => new Alpaca.Markets.BarTimeFrame(5, Alpaca.Markets.BarTimeFrameUnit.Minute),
        Core.Enums.BarTimeFrame.FifteenMinutes => new Alpaca.Markets.BarTimeFrame(15, Alpaca.Markets.BarTimeFrameUnit.Minute),
        Core.Enums.BarTimeFrame.Hour => Alpaca.Markets.BarTimeFrame.Hour,
        Core.Enums.BarTimeFrame.Week => Alpaca.Markets.BarTimeFrame.Week,
        Core.Enums.BarTimeFrame.Month => Alpaca.Markets.BarTimeFrame.Month,
        _ => Alpaca.Markets.BarTimeFrame.Day
    };

    /// <summary>
    /// Gets historical daily close prices for a symbol (cached for 30s).
    /// Returns an array with the most recent price last.
    /// </summary>
    public virtual async Task<decimal[]> GetHistoricalPricesAsync(string symbol, int barCount = 50) =>
        await GetHistoricalPricesAsync(symbol, Core.Enums.BarTimeFrame.Day, barCount);

    /// <summary>
    /// Gets historical close prices for a symbol at the specified interval (cached for 30s).
    /// Returns an array with the most recent price last.
    /// If insufficient bars are available at the requested interval, falls back to daily bars.
    /// </summary>
    public virtual async Task<decimal[]> GetHistoricalPricesAsync(string symbol, Core.Enums.BarTimeFrame timeFrame, int barCount = 50)
    {
        var cacheKey = $"{symbol}:{timeFrame}:{barCount}";
        if (_historyCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.fetchedUtc < CacheTtl)
            return cached.prices;

        try
        {
            // Use a generous lookback to ensure enough bars
            var lookbackDays = timeFrame switch
            {
                Core.Enums.BarTimeFrame.OneMinute or Core.Enums.BarTimeFrame.FiveMinutes => 10,
                Core.Enums.BarTimeFrame.FifteenMinutes => 14,
                Core.Enums.BarTimeFrame.Hour => 30,
                Core.Enums.BarTimeFrame.Week => barCount * 10,
                Core.Enums.BarTimeFrame.Month => barCount * 35,
                _ => barCount * 2
            };
            var to = DateOnly.FromDateTime(DateTime.UtcNow);
            var from = to.AddDays(-lookbackDays);
            var bars = await GetHistoricalBarsAsync(symbol, from, to, timeFrame);
            var allPrices = bars.Select(b => b.Close).ToArray();

            // Take the most recent barCount prices
            var prices = allPrices.Length > barCount
                ? allPrices[^barCount..]
                : allPrices;

            _logger.LogInformation("Fetched {Count} historical bars for {Symbol} ({TimeFrame})", prices.Length, symbol, timeFrame);

            // If not enough bars at this interval, prepend daily closes so indicators
            // have enough history while the most recent data points stay intraday
            if (prices.Length < barCount && timeFrame != Core.Enums.BarTimeFrame.Day)
            {
                var needed = barCount - prices.Length;
                _logger.LogInformation("Only {Count}/{Needed} bars at {TimeFrame} for {Symbol}, prepending {Fill} daily closes",
                    prices.Length, barCount, timeFrame, symbol, needed);
                var dailyPrices = await GetHistoricalPricesAsync(symbol, Core.Enums.BarTimeFrame.Day, needed);
                prices = [.. dailyPrices, .. prices];
            }

            _historyCache[cacheKey] = (prices, DateTime.UtcNow);
            return prices;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch historical prices for {Symbol} ({TimeFrame})", symbol, timeFrame);
            return [];
        }
    }
}
