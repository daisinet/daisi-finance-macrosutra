using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class MoomooBrokerageProviderTests
{
    [Theory]
    [InlineData("FILLED_ALL", TradeStatus.Filled)]
    [InlineData("FILLED_PART", TradeStatus.PartiallyFilled)]
    [InlineData("CANCELLED_ALL", TradeStatus.Cancelled)]
    [InlineData("CANCELLED_PART", TradeStatus.Cancelled)]
    [InlineData("DELETED", TradeStatus.Cancelled)]
    [InlineData("SUBMITTED", TradeStatus.Submitted)]
    [InlineData("SUBMITTING", TradeStatus.Submitted)]
    [InlineData("NONE", TradeStatus.Pending)]
    [InlineData("UNSUBMITTED", TradeStatus.Pending)]
    [InlineData("WAITING_SUBMIT", TradeStatus.Pending)]
    [InlineData("FAILED", TradeStatus.Failed)]
    [InlineData("DISABLED", TradeStatus.Failed)]
    [InlineData("UNKNOWN", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string moomooStatus, TradeStatus expected)
    {
        var result = MoomooBrokerageProvider.MapOrderStatus(moomooStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"Host":"192.168.1.1","Port":11111,"TradingPassword":"pass","SecurityFirm":"FutuSecurities","IsPaperTrading":true}""";

        var (creds, isPaper) = MoomooBrokerageProvider.ParseCredentials(json);

        Assert.Equal("192.168.1.1", creds.Host);
        Assert.Equal(11111, creds.Port);
        Assert.Equal("pass", creds.TradingPassword);
        Assert.Equal("FutuSecurities", creds.SecurityFirm);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_MissingFields_UsesDefaults()
    {
        var json = """{}""";

        var (creds, isPaper) = MoomooBrokerageProvider.ParseCredentials(json);

        Assert.Equal("127.0.0.1", creds.Host);
        Assert.Equal(11111, creds.Port);
        Assert.Equal("", creds.TradingPassword);
        Assert.Equal("FutuSecurities", creds.SecurityFirm);
        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_LiveMode_ReturnsFalse()
    {
        var json = """{"Host":"127.0.0.1","Port":11111,"TradingPassword":"p","IsPaperTrading":false}""";

        var (creds, isPaper) = MoomooBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => MoomooBrokerageProvider.ParseCredentials("invalid"));
    }
}
