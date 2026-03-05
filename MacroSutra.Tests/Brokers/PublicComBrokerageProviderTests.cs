using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class PublicComBrokerageProviderTests
{
    [Theory]
    [InlineData("filled", TradeStatus.Filled)]
    [InlineData("partially_filled", TradeStatus.PartiallyFilled)]
    [InlineData("canceled", TradeStatus.Cancelled)]
    [InlineData("cancelled", TradeStatus.Cancelled)]
    [InlineData("rejected", TradeStatus.Rejected)]
    [InlineData("pending", TradeStatus.Pending)]
    [InlineData("new", TradeStatus.Pending)]
    [InlineData("open", TradeStatus.Submitted)]
    [InlineData("accepted", TradeStatus.Submitted)]
    [InlineData("expired", TradeStatus.Cancelled)]
    [InlineData("unknown", TradeStatus.Pending)]
    public void MapOrderStatus_CorrectlyMaps(string publicStatus, TradeStatus expected)
    {
        var result = PublicComBrokerageProvider.MapOrderStatus(publicStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"ApiKey":"AKTEST","SecretKey":"SKTEST","IsPaperTrading":true}""";

        var (creds, isPaper) = PublicComBrokerageProvider.ParseCredentials(json);

        Assert.Equal("AKTEST", creds.ApiKey);
        Assert.Equal("SKTEST", creds.SecretKey);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_LiveMode_ReturnsFalse()
    {
        var json = """{"ApiKey":"AK","SecretKey":"SK","IsPaperTrading":false}""";

        var (creds, isPaper) = PublicComBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_MissingPaperFlag_DefaultsFalse()
    {
        var json = """{"ApiKey":"AK","SecretKey":"SK"}""";

        var (creds, isPaper) = PublicComBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => PublicComBrokerageProvider.ParseCredentials("not json"));
    }
}
