using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class SchwabBrokerageProviderTests
{
    [Theory]
    [InlineData("FILLED", TradeStatus.Filled)]
    [InlineData("PARTIALLY_FILLED", TradeStatus.PartiallyFilled)]
    [InlineData("CANCELED", TradeStatus.Cancelled)]
    [InlineData("CANCELLED", TradeStatus.Cancelled)]
    [InlineData("REJECTED", TradeStatus.Rejected)]
    [InlineData("WORKING", TradeStatus.Submitted)]
    [InlineData("ACCEPTED", TradeStatus.Submitted)]
    [InlineData("PENDING_ACTIVATION", TradeStatus.Pending)]
    [InlineData("QUEUED", TradeStatus.Pending)]
    [InlineData("EXPIRED", TradeStatus.Cancelled)]
    [InlineData("PENDING_CANCEL", TradeStatus.Submitted)]
    [InlineData("PENDING_REPLACE", TradeStatus.Submitted)]
    [InlineData("UNKNOWN", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string schwabStatus, TradeStatus expected)
    {
        var result = SchwabBrokerageProvider.MapOrderStatus(schwabStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"AppKey":"AK","AppSecret":"AS","AccessToken":"AT","RefreshToken":"RT","IsPaperTrading":true}""";

        var (creds, isPaper) = SchwabBrokerageProvider.ParseCredentials(json);

        Assert.Equal("AK", creds.AppKey);
        Assert.Equal("AS", creds.AppSecret);
        Assert.Equal("AT", creds.AccessToken);
        Assert.Equal("RT", creds.RefreshToken);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_LiveMode_ReturnsFalse()
    {
        var json = """{"AppKey":"AK","AppSecret":"AS","AccessToken":"AT","RefreshToken":"RT","IsPaperTrading":false}""";

        var (creds, isPaper) = SchwabBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_WithTokenExpiry_ParsesDateTime()
    {
        var json = """{"AppKey":"AK","AppSecret":"AS","AccessToken":"AT","RefreshToken":"RT","TokenExpiresUtc":"2026-12-31T23:59:59Z","IsPaperTrading":false}""";

        var (creds, _) = SchwabBrokerageProvider.ParseCredentials(json);

        Assert.NotNull(creds.TokenExpiresUtc);
        Assert.Equal(2026, creds.TokenExpiresUtc!.Value.Year);
    }

    [Fact]
    public void ParseCredentials_MissingOptionalFields_DefaultsCorrectly()
    {
        var json = """{"AppKey":"AK","AppSecret":"AS","AccessToken":"AT"}""";

        var (creds, isPaper) = SchwabBrokerageProvider.ParseCredentials(json);

        Assert.Equal("", creds.RefreshToken);
        Assert.Null(creds.TokenExpiresUtc);
        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => SchwabBrokerageProvider.ParseCredentials("invalid"));
    }
}
