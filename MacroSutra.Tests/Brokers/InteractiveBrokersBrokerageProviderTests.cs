using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class InteractiveBrokersBrokerageProviderTests
{
    [Theory]
    [InlineData("FILLED", TradeStatus.Filled)]
    [InlineData("PARTIALFILLED", TradeStatus.PartiallyFilled)]
    [InlineData("CANCELLED", TradeStatus.Cancelled)]
    [InlineData("CANCELED", TradeStatus.Cancelled)]
    [InlineData("INACTIVE", TradeStatus.Cancelled)]
    [InlineData("APICANCELLED", TradeStatus.Cancelled)]
    [InlineData("SUBMITTED", TradeStatus.Submitted)]
    [InlineData("PRESUBMITTED", TradeStatus.Pending)]
    [InlineData("PENDINGSUBMIT", TradeStatus.Pending)]
    [InlineData("ERROR", TradeStatus.Failed)]
    [InlineData("UNKNOWN", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string ibkrStatus, TradeStatus expected)
    {
        var result = InteractiveBrokersBrokerageProvider.MapOrderStatus(ibkrStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"Host":"192.168.1.1","Port":7496,"ClientId":2,"IsPaperTrading":false}""";

        var (creds, isPaper) = InteractiveBrokersBrokerageProvider.ParseCredentials(json);

        Assert.Equal("192.168.1.1", creds.Host);
        Assert.Equal(7496, creds.Port);
        Assert.Equal(2, creds.ClientId);
        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_PaperPort_DetectsPaperMode()
    {
        var json = """{"Host":"127.0.0.1","Port":7497,"ClientId":1}""";

        var (creds, isPaper) = InteractiveBrokersBrokerageProvider.ParseCredentials(json);

        Assert.Equal(7497, creds.Port);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_MissingFields_UsesDefaults()
    {
        var json = """{}""";

        var (creds, isPaper) = InteractiveBrokersBrokerageProvider.ParseCredentials(json);

        Assert.Equal("127.0.0.1", creds.Host);
        Assert.Equal(7497, creds.Port);
        Assert.Equal(1, creds.ClientId);
        Assert.True(isPaper); // Port 7497 = paper
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => InteractiveBrokersBrokerageProvider.ParseCredentials("invalid"));
    }
}
