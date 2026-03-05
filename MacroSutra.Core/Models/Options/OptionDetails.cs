using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models.Options;

/// <summary>
/// Option-specific details attached to a Trade or Position.
/// Nullable on Trade/Position for backward compatibility with equity orders.
/// </summary>
public class OptionDetails
{
    public string ContractSymbol { get; set; } = "";
    public string UnderlyingSymbol { get; set; } = "";
    public OptionType OptionType { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal StrikePrice { get; set; }
    public int Contracts { get; set; }
    public decimal? PremiumPerShare { get; set; }

    /// <summary>
    /// Optional Greeks snapshot at time of trade/position sync.
    /// </summary>
    public OptionGreeks? Greeks { get; set; }
}

/// <summary>
/// Option Greeks for a contract.
/// </summary>
public class OptionGreeks
{
    public decimal? Delta { get; set; }
    public decimal? Gamma { get; set; }
    public decimal? Theta { get; set; }
    public decimal? Vega { get; set; }
    public decimal? ImpliedVolatility { get; set; }
}
