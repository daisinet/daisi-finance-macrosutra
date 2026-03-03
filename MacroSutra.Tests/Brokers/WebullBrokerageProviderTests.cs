using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class WebullBrokerageProviderTests
{
    [Theory]
    [InlineData("FILLED", TradeStatus.Filled)]
    [InlineData("PARTIALLY_FILLED", TradeStatus.PartiallyFilled)]
    [InlineData("CANCELLED", TradeStatus.Cancelled)]
    [InlineData("CANCELED", TradeStatus.Cancelled)]
    [InlineData("REJECTED", TradeStatus.Rejected)]
    [InlineData("FAILED", TradeStatus.Failed)]
    [InlineData("PENDING", TradeStatus.Pending)]
    [InlineData("WORKING", TradeStatus.Submitted)]
    [InlineData("UNKNOWN", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string webullStatus, TradeStatus expected)
    {
        var result = WebullBrokerageProvider.MapOrderStatus(webullStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"AppKey":"AK","AppSecret":"AS","AccessToken":"AT","RefreshToken":"RT","IsPaperTrading":true}""";

        var (creds, isPaper) = WebullBrokerageProvider.ParseCredentials(json);

        Assert.Equal("AK", creds.AppKey);
        Assert.Equal("AS", creds.AppSecret);
        Assert.Equal("AT", creds.AccessToken);
        Assert.Equal("RT", creds.RefreshToken);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_LiveMode_ReturnsFalse()
    {
        var json = """{"AppKey":"AK","AppSecret":"AS","AccessToken":"AT","IsPaperTrading":false}""";

        var (creds, isPaper) = WebullBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_WithTokenExpiry_ParsesDateTime()
    {
        var json = """{"AppKey":"AK","AppSecret":"AS","AccessToken":"AT","RefreshToken":"RT","TokenExpiresUtc":"2026-12-31T23:59:59Z","IsPaperTrading":false}""";

        var (creds, _) = WebullBrokerageProvider.ParseCredentials(json);

        Assert.NotNull(creds.TokenExpiresUtc);
        Assert.Equal(2026, creds.TokenExpiresUtc!.Value.Year);
    }

    [Fact]
    public void ParseCredentials_MissingOptionalFields_DefaultsCorrectly()
    {
        var json = """{"AppKey":"AK","AppSecret":"AS","AccessToken":"AT"}""";

        var (creds, isPaper) = WebullBrokerageProvider.ParseCredentials(json);

        Assert.Equal("", creds.RefreshToken);
        Assert.Null(creds.TokenExpiresUtc);
        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => WebullBrokerageProvider.ParseCredentials("invalid"));
    }
}
