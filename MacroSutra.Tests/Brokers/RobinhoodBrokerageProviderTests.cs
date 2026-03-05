using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class RobinhoodBrokerageProviderTests
{
    [Theory]
    [InlineData("filled", TradeStatus.Filled)]
    [InlineData("partially_filled", TradeStatus.PartiallyFilled)]
    [InlineData("canceled", TradeStatus.Cancelled)]
    [InlineData("cancelled", TradeStatus.Cancelled)]
    [InlineData("rejected", TradeStatus.Rejected)]
    [InlineData("failed", TradeStatus.Rejected)]
    [InlineData("pending", TradeStatus.Pending)]
    [InlineData("queued", TradeStatus.Pending)]
    [InlineData("unconfirmed", TradeStatus.Pending)]
    [InlineData("confirmed", TradeStatus.Submitted)]
    [InlineData("placed", TradeStatus.Submitted)]
    [InlineData("expired", TradeStatus.Cancelled)]
    [InlineData("unknown", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string rhStatus, TradeStatus expected)
    {
        var result = RobinhoodBrokerageProvider.MapOrderStatus(rhStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"ApiKey":"AKTEST","ApiSecret":"ASTEST","Base64PrivateKey":"cHJpdmtleQ==","IsPaperTrading":false}""";

        var (creds, isPaper) = RobinhoodBrokerageProvider.ParseCredentials(json);

        Assert.Equal("AKTEST", creds.ApiKey);
        Assert.Equal("ASTEST", creds.ApiSecret);
        Assert.Equal("cHJpdmtleQ==", creds.Base64PrivateKey);
        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_MissingFields_DefaultsCorrectly()
    {
        var json = """{"ApiKey":"AK"}""";

        var (creds, isPaper) = RobinhoodBrokerageProvider.ParseCredentials(json);

        Assert.Equal("AK", creds.ApiKey);
        Assert.Equal("", creds.ApiSecret);
        Assert.Equal("", creds.Base64PrivateKey);
        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => RobinhoodBrokerageProvider.ParseCredentials("invalid"));
    }
}
