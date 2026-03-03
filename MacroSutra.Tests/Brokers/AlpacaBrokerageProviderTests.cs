using Alpaca.Markets;
using MacroSutra.Brokers;
using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Brokers;

public class AlpacaBrokerageProviderTests
{
    [Theory]
    [InlineData(OrderStatus.Filled, TradeStatus.Filled)]
    [InlineData(OrderStatus.PartiallyFilled, TradeStatus.PartiallyFilled)]
    [InlineData(OrderStatus.Canceled, TradeStatus.Cancelled)]
    [InlineData(OrderStatus.Rejected, TradeStatus.Rejected)]
    [InlineData(OrderStatus.New, TradeStatus.Submitted)]
    [InlineData(OrderStatus.Accepted, TradeStatus.Submitted)]
    [InlineData(OrderStatus.PendingNew, TradeStatus.Pending)]
    [InlineData(OrderStatus.Expired, TradeStatus.Cancelled)]
    [InlineData(OrderStatus.Stopped, TradeStatus.Cancelled)]
    public void MapOrderStatus_CorrectlyMaps(OrderStatus alpacaStatus, TradeStatus expected)
    {
        var result = AlpacaBrokerageProvider.MapOrderStatus(alpacaStatus);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseCredentials_ValidJson_ReturnsCreds()
    {
        var json = """{"ApiKey":"AKTEST","SecretKey":"SKTEST","IsPaperTrading":true}""";

        var (creds, isPaper) = AlpacaBrokerageProvider.ParseCredentials(json);

        Assert.Equal("AKTEST", creds.ApiKey);
        Assert.Equal("SKTEST", creds.SecretKey);
        Assert.True(isPaper);
    }

    [Fact]
    public void ParseCredentials_LiveMode_ReturnsFalse()
    {
        var json = """{"ApiKey":"AK","SecretKey":"SK","IsPaperTrading":false}""";

        var (creds, isPaper) = AlpacaBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_MissingPaperFlag_DefaultsFalse()
    {
        var json = """{"ApiKey":"AK","SecretKey":"SK"}""";

        var (creds, isPaper) = AlpacaBrokerageProvider.ParseCredentials(json);

        Assert.False(isPaper);
    }

    [Fact]
    public void ParseCredentials_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => AlpacaBrokerageProvider.ParseCredentials("not json"));
    }
}
