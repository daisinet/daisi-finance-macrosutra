using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class TradeStationBrokerageProviderTests
{
    [Theory]
    [InlineData("FLL", TradeStatus.Filled)]
    [InlineData("FPR", TradeStatus.PartiallyFilled)]
    [InlineData("CAN", TradeStatus.Cancelled)]
    [InlineData("OUT", TradeStatus.Cancelled)]
    [InlineData("EXP", TradeStatus.Cancelled)]
    [InlineData("REJ", TradeStatus.Rejected)]
    [InlineData("UCN", TradeStatus.Rejected)]
    [InlineData("ACK", TradeStatus.Pending)]
    [InlineData("OPN", TradeStatus.Submitted)]
    [InlineData("DON", TradeStatus.Submitted)]
    [InlineData("BRO", TradeStatus.Failed)]
    [InlineData("UNKNOWN", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string tsStatus, TradeStatus expected)
    {
        var result = TradeStationBrokerageProvider.MapOrderStatus(tsStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"ClientId":"CID","ClientSecret":"CS","AccessToken":"AT","RefreshToken":"RT","IsPaperTrading":true}""";

        var (creds, isPaper) = TradeStationBrokerageProvider.ParseCredentials(json);

        Assert.Equal("CID", creds.ClientId);
        Assert.Equal("CS", creds.ClientSecret);
        Assert.Equal("AT", creds.AccessToken);
        Assert.Equal("RT", creds.RefreshToken);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_LiveMode_ReturnsFalse()
    {
        var json = """{"ClientId":"CID","ClientSecret":"CS","AccessToken":"AT","RefreshToken":"RT","IsPaperTrading":false}""";

        var (creds, isPaper) = TradeStationBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_WithTokenExpiry_ParsesDateTime()
    {
        var json = """{"ClientId":"CID","ClientSecret":"CS","AccessToken":"AT","RefreshToken":"RT","TokenExpiresUtc":"2026-12-31T23:59:59Z","IsPaperTrading":false}""";

        var (creds, _) = TradeStationBrokerageProvider.ParseCredentials(json);

        Assert.NotNull(creds.TokenExpiresUtc);
        Assert.Equal(2026, creds.TokenExpiresUtc!.Value.Year);
    }

    [Fact]
    public void ParseCredentials_MissingOptionalFields_DefaultsCorrectly()
    {
        var json = """{"ClientId":"CID","ClientSecret":"CS","AccessToken":"AT"}""";

        var (creds, isPaper) = TradeStationBrokerageProvider.ParseCredentials(json);

        Assert.Equal("", creds.RefreshToken);
        Assert.Null(creds.TokenExpiresUtc);
        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => TradeStationBrokerageProvider.ParseCredentials("invalid"));
    }
}
