namespace MacroSutra.Core.Models;

/// <summary>
/// Historical OHLCV bar. For daily bars, Date is the trading day and Timestamp is midnight.
/// For intraday bars, Timestamp contains the precise bar time.
/// </summary>
public record OhlcvBar(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume)
{
    /// <summary>
    /// Precise bar timestamp for intraday bars. Defaults to start of Date for daily bars.
    /// </summary>
    public DateTime Timestamp { get; init; } = Date.ToDateTime(TimeOnly.MinValue);
}
