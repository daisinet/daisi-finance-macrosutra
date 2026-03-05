namespace MacroSutra.Core.Models.Options;

/// <summary>
/// An options chain for a given underlying symbol.
/// Contains one or more expirations, each with calls and puts.
/// </summary>
public class OptionsChain
{
    public string UnderlyingSymbol { get; set; } = "";
    public decimal UnderlyingPrice { get; set; }
    public List<OptionsExpiration> Expirations { get; set; } = new();
}

/// <summary>
/// Options contracts for a single expiration date.
/// </summary>
public class OptionsExpiration
{
    public DateOnly ExpirationDate { get; set; }
    public List<OptionContract> Calls { get; set; } = new();
    public List<OptionContract> Puts { get; set; } = new();
}

/// <summary>
/// A single options contract in the chain.
/// </summary>
public class OptionContract
{
    public string ContractSymbol { get; set; } = "";
    public decimal StrikePrice { get; set; }
    public decimal? Bid { get; set; }
    public decimal? Ask { get; set; }
    public decimal? LastPrice { get; set; }
    public int OpenInterest { get; set; }
    public int Volume { get; set; }
    public OptionGreeks? Greeks { get; set; }
}
