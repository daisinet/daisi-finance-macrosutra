using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class TradierBrokerageProviderTests
{
    [Theory]
    [InlineData("filled", TradeStatus.Filled)]
    [InlineData("partially_filled", TradeStatus.PartiallyFilled)]
    [InlineData("canceled", TradeStatus.Cancelled)]
    [InlineData("cancelled", TradeStatus.Cancelled)]
    [InlineData("rejected", TradeStatus.Rejected)]
    [InlineData("pending", TradeStatus.Pending)]
    [InlineData("open", TradeStatus.Submitted)]
    [InlineData("expired", TradeStatus.Cancelled)]
    [InlineData("unknown", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string tradierStatus, TradeStatus expected)
    {
        var result = TradierBrokerageProvider.MapOrderStatus(tradierStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"AccessToken":"TOKEN123","AccountNumber":"ACC456","IsPaperTrading":true}""";

        var (creds, isPaper) = TradierBrokerageProvider.ParseCredentials(json);

        Assert.Equal("TOKEN123", creds.AccessToken);
        Assert.Equal("ACC456", creds.AccountNumber);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_LiveMode_ReturnsFalse()
    {
        var json = """{"AccessToken":"TOKEN","AccountNumber":"ACC","IsPaperTrading":false}""";

        var (creds, isPaper) = TradierBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_MissingPaperFlag_DefaultsFalse()
    {
        var json = """{"AccessToken":"TOKEN","AccountNumber":"ACC"}""";

        var (creds, isPaper) = TradierBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => TradierBrokerageProvider.ParseCredentials("not json"));
    }
}
