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
    private readonly ConcurrentDictionary<string, (MarketSnapshot snapshot, DateTime fetchedUtc)> _snapshotCache = new();
    private readonly ConcurrentDictionary<string, (decimal[] prices, DateTime fetchedUtc)> _historyCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public MarketDataService(IConfiguration configuration, ILogger<MarketDataService> logger)
    {
        _logger = logger;

        var apiKey = configuration["Alpaca:DataApiKey"] ?? configuration["Alpaca:ApiKey"] ?? "";
        var secretKey = configuration["Alpaca:DataSecretKey"] ?? configuration["Alpaca:SecretKey"] ?? "";

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
            var snapshot = await _dataClient.GetSnapshotAsync(new LatestMarketDataRequest(symbol));

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
            var request = new HistoricalBarsRequest(symbol,
                from.ToDateTime(TimeOnly.MinValue),
                to.ToDateTime(TimeOnly.MinValue),
                alpacaTimeFrame);

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
        Core.Enums.BarTimeFrame.Hour => Alpaca.Markets.BarTimeFrame.Hour,
        Core.Enums.BarTimeFrame.FifteenMinutes => new Alpaca.Markets.BarTimeFrame(15, Alpaca.Markets.BarTimeFrameUnit.Minute),
        Core.Enums.BarTimeFrame.FiveMinutes => new Alpaca.Markets.BarTimeFrame(5, Alpaca.Markets.BarTimeFrameUnit.Minute),
        Core.Enums.BarTimeFrame.OneMinute => Alpaca.Markets.BarTimeFrame.Minute,
        _ => Alpaca.Markets.BarTimeFrame.Day
    };

    /// <summary>
    /// Gets historical daily close prices for a symbol (cached for 30s).
    /// Returns an array with the most recent price last.
    /// </summary>
    public virtual async Task<decimal[]> GetHistoricalPricesAsync(string symbol, int barCount = 50)
    {
        var cacheKey = $"{symbol}:{barCount}";
        if (_historyCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.fetchedUtc < CacheTtl)
            return cached.prices;

        try
        {
            var request = new HistoricalBarsRequest(symbol, Alpaca.Markets.BarTimeFrame.Day)
                .WithPageSize((uint)barCount);

            var bars = await _dataClient.ListHistoricalBarsAsync(request);
            var prices = bars.Items.Select(b => b.Close).ToArray();

            _historyCache[cacheKey] = (prices, DateTime.UtcNow);
            return prices;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch historical prices for {Symbol}", symbol);
            return [];
        }
    }
}
