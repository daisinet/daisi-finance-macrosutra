using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class TastytradeBrokerageProviderTests
{
    [Theory]
    [InlineData("Filled", TradeStatus.Filled)]
    [InlineData("Partially Filled", TradeStatus.PartiallyFilled)]
    [InlineData("Cancelled", TradeStatus.Cancelled)]
    [InlineData("Canceled", TradeStatus.Cancelled)]
    [InlineData("Rejected", TradeStatus.Rejected)]
    [InlineData("Received", TradeStatus.Pending)]
    [InlineData("Routed", TradeStatus.Submitted)]
    [InlineData("Live", TradeStatus.Submitted)]
    [InlineData("In Flight", TradeStatus.Submitted)]
    [InlineData("Expired", TradeStatus.Cancelled)]
    [InlineData("Unknown", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string tastytradeStatus, TradeStatus expected)
    {
        var result = TastytradeBrokerageProvider.MapOrderStatus(tastytradeStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"Username":"user@test.com","Password":"pass123","SessionToken":"TOKEN","IsPaperTrading":true}""";

        var (creds, isPaper) = TastytradeBrokerageProvider.ParseCredentials(json);

        Assert.Equal("user@test.com", creds.Username);
        Assert.Equal("pass123", creds.Password);
        Assert.Equal("TOKEN", creds.SessionToken);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_LiveMode_ReturnsFalse()
    {
        var json = """{"Username":"user","Password":"pass","SessionToken":"ST","IsPaperTrading":false}""";

        var (creds, isPaper) = TastytradeBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_WithTokenExpiry_ParsesDateTime()
    {
        var json = """{"Username":"u","Password":"p","SessionToken":"ST","TokenExpiresUtc":"2026-12-31T23:59:59Z","IsPaperTrading":false}""";

        var (creds, _) = TastytradeBrokerageProvider.ParseCredentials(json);

        Assert.NotNull(creds.TokenExpiresUtc);
        Assert.Equal(2026, creds.TokenExpiresUtc!.Value.Year);
    }

    [Fact]
    public void ParseCredentials_MissingOptionalFields_DefaultsCorrectly()
    {
        var json = """{"Username":"u","Password":"p"}""";

        var (creds, isPaper) = TastytradeBrokerageProvider.ParseCredentials(json);

        Assert.Equal("", creds.SessionToken);
        Assert.Null(creds.TokenExpiresUtc);
        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => TastytradeBrokerageProvider.ParseCredentials("invalid"));
    }
}
