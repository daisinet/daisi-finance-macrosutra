using MacroSutra.Services;

namespace MacroSutra.Tests.Services;

public class TechnicalIndicatorsTests
{
    [Fact]
    public void SMA_KnownValues_ReturnsCorrectAverage()
    {
        var prices = new decimal[] { 10, 11, 12, 13, 14 };
        var result = TechnicalIndicators.SMA(prices, 5);
        Assert.Equal(12m, result);
    }

    [Fact]
    public void SMA_PartialPeriod_UsesLastNValues()
    {
        var prices = new decimal[] { 1, 2, 3, 10, 20, 30 };
        var result = TechnicalIndicators.SMA(prices, 3);
        Assert.Equal(20m, result); // (10 + 20 + 30) / 3
    }

    [Fact]
    public void SMA_InsufficientData_Throws()
    {
        var prices = new decimal[] { 10, 20 };
        Assert.Throws<ArgumentException>(() => TechnicalIndicators.SMA(prices, 5));
    }

    [Fact]
    public void EMA_KnownValues_ReturnsReasonableResult()
    {
        // 10-period EMA of a simple ascending series
        var prices = new decimal[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var result = TechnicalIndicators.EMA(prices, 10);

        // EMA should be between the min and max
        Assert.True(result > 1 && result <= 10);
    }

    [Fact]
    public void EMA_InsufficientData_Throws()
    {
        var prices = new decimal[] { 10, 20 };
        Assert.Throws<ArgumentException>(() => TechnicalIndicators.EMA(prices, 5));
    }

    [Fact]
    public void RSI_AllGains_Returns100()
    {
        // Steadily rising prices — RSI should be 100
        var prices = new decimal[] { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 };
        var result = TechnicalIndicators.RSI(prices, 14);
        Assert.Equal(100m, result);
    }

    [Fact]
    public void RSI_AllLosses_Returns0()
    {
        // Steadily falling prices — RSI should be 0
        var prices = new decimal[] { 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10 };
        var result = TechnicalIndicators.RSI(prices, 14);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void RSI_MixedPrices_ReturnsBetween0And100()
    {
        var prices = new decimal[] { 44, 44.34m, 44.09m, 43.61m, 44.33m, 44.83m, 45.10m, 45.42m, 45.84m, 46.08m,
                                      45.89m, 46.03m, 45.61m, 46.28m, 46.28m };
        var result = TechnicalIndicators.RSI(prices, 14);
        Assert.True(result > 0 && result < 100);
    }

    [Fact]
    public void RSI_InsufficientData_Throws()
    {
        var prices = new decimal[] { 10, 20, 30 };
        Assert.Throws<ArgumentException>(() => TechnicalIndicators.RSI(prices, 14));
    }

    [Fact]
    public void MACD_KnownValues_ReturnsThreeComponents()
    {
        // Generate enough data (at least 26 values)
        var prices = Enumerable.Range(1, 30).Select(i => (decimal)i).ToArray();
        var (macdLine, signalLine, histogram) = TechnicalIndicators.MACD(prices);

        // MACD of a linear series: fast EMA > slow EMA since fast reacts more
        Assert.True(macdLine > 0);
        Assert.Equal(macdLine - signalLine, histogram);
    }

    [Fact]
    public void MACD_InsufficientData_Throws()
    {
        var prices = new decimal[] { 10, 20, 30 };
        Assert.Throws<ArgumentException>(() => TechnicalIndicators.MACD(prices));
    }
}
