namespace MacroSutra.Services;

/// <summary>
/// Pure static utility for computing technical indicators from price arrays.
/// All methods take a prices array with the most recent value last.
/// </summary>
public static class TechnicalIndicators
{
    /// <summary>
    /// Simple Moving Average over the specified period.
    /// </summary>
    public static decimal SMA(decimal[] prices, int period)
    {
        if (prices.Length < period || period <= 0)
            throw new ArgumentException($"Need at least {period} prices for SMA({period}), got {prices.Length}.");

        decimal sum = 0;
        for (int i = prices.Length - period; i < prices.Length; i++)
            sum += prices[i];

        return sum / period;
    }

    /// <summary>
    /// Exponential Moving Average over the specified period.
    /// </summary>
    public static decimal EMA(decimal[] prices, int period)
    {
        if (prices.Length < period || period <= 0)
            throw new ArgumentException($"Need at least {period} prices for EMA({period}), got {prices.Length}.");

        decimal multiplier = 2m / (period + 1);

        // Seed with SMA of first 'period' values
        decimal ema = 0;
        for (int i = 0; i < period; i++)
            ema += prices[i];
        ema /= period;

        // Apply EMA formula for remaining values
        for (int i = period; i < prices.Length; i++)
            ema = (prices[i] - ema) * multiplier + ema;

        return ema;
    }

    /// <summary>
    /// Relative Strength Index over the specified period. Returns 0-100.
    /// </summary>
    public static decimal RSI(decimal[] prices, int period)
    {
        if (prices.Length < period + 1 || period <= 0)
            throw new ArgumentException($"Need at least {period + 1} prices for RSI({period}), got {prices.Length}.");

        decimal avgGain = 0, avgLoss = 0;

        // Initial average gain/loss over first 'period' changes
        for (int i = 1; i <= period; i++)
        {
            var change = prices[i] - prices[i - 1];
            if (change > 0) avgGain += change;
            else avgLoss += Math.Abs(change);
        }
        avgGain /= period;
        avgLoss /= period;

        // Smoothed RSI for remaining values
        for (int i = period + 1; i < prices.Length; i++)
        {
            var change = prices[i] - prices[i - 1];
            if (change > 0)
            {
                avgGain = (avgGain * (period - 1) + change) / period;
                avgLoss = (avgLoss * (period - 1)) / period;
            }
            else
            {
                avgGain = (avgGain * (period - 1)) / period;
                avgLoss = (avgLoss * (period - 1) + Math.Abs(change)) / period;
            }
        }

        if (avgLoss == 0) return 100;
        var rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }

    /// <summary>
    /// MACD indicator. Returns (macdLine, signalLine, histogram).
    /// </summary>
    public static (decimal macdLine, decimal signalLine, decimal histogram) MACD(
        decimal[] prices, int fast = 12, int slow = 26, int signal = 9)
    {
        if (prices.Length < slow)
            throw new ArgumentException($"Need at least {slow} prices for MACD, got {prices.Length}.");

        var fastEma = EMA(prices, fast);
        var slowEma = EMA(prices, slow);
        var macdLine = fastEma - slowEma;

        // Build MACD line series for signal line calculation
        var macdSeries = new decimal[prices.Length - slow + 1];
        for (int i = 0; i < macdSeries.Length; i++)
        {
            var slice = prices[..(slow + i)];
            var f = EMA(slice, fast);
            var s = EMA(slice, slow);
            macdSeries[i] = f - s;
        }

        decimal signalLine;
        if (macdSeries.Length >= signal)
            signalLine = EMA(macdSeries, signal);
        else
            signalLine = macdSeries[^1];

        return (macdLine, signalLine, macdLine - signalLine);
    }
}
