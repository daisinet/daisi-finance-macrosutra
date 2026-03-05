using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tests.Brokers;

/// <summary>
/// WU1: Fractional shares awareness flag tests.
/// WU8: Options support flag tests.
/// </summary>
public class FractionalAndOptionsTests
{
    private static BrokerageProviderFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton<PaperBrokerageProvider>();
        services.AddSingleton<AlpacaBrokerageProvider>();
        services.AddSingleton<InteractiveBrokersBrokerageProvider>();
        services.AddSingleton<MoomooBrokerageProvider>();
        services.AddHttpClient("Webull");
        services.AddHttpClient("Schwab");
        services.AddHttpClient("Tradier");
        services.AddHttpClient("Tastytrade");
        services.AddHttpClient("TradeStation");
        services.AddHttpClient("PublicCom");
        services.AddHttpClient("Robinhood");
        services.AddSingleton<WebullBrokerageProvider>();
        services.AddSingleton<SchwabBrokerageProvider>();
        services.AddSingleton<TradierBrokerageProvider>();
        services.AddSingleton<TastytradeBrokerageProvider>();
        services.AddSingleton<TradeStationBrokerageProvider>();
        services.AddSingleton<PublicComBrokerageProvider>();
        services.AddSingleton<RobinhoodBrokerageProvider>();
        var sp = services.BuildServiceProvider();
        return new BrokerageProviderFactory(sp);
    }

    // ── Fractional Shares ──

    [Fact]
    public void Alpaca_SupportsFractionalShares()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Alpaca);
        Assert.True(provider.SupportsFractionalShares);
    }

    [Fact]
    public void Paper_SupportsFractionalShares()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Paper);
        Assert.True(provider.SupportsFractionalShares);
    }

    [Fact]
    public void Webull_DoesNotSupportFractionalShares()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Webull);
        Assert.False(provider.SupportsFractionalShares);
    }

    [Fact]
    public void PublicCom_SupportsFractionalShares()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.PublicCom);
        Assert.True(provider.SupportsFractionalShares);
    }

    // ── Options Support ──

    [Fact]
    public void Alpaca_SupportsOptions()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Alpaca);
        Assert.True(provider.SupportsOptions);
    }

    [Fact]
    public void Tastytrade_SupportsOptions()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Tastytrade);
        Assert.True(provider.SupportsOptions);
    }

    [Fact]
    public void Webull_DoesNotSupportOptions()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Webull);
        Assert.False(provider.SupportsOptions);
    }

    [Fact]
    public void Paper_DoesNotSupportOptions()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Paper);
        Assert.False(provider.SupportsOptions);
    }

    [Fact]
    public async Task NonOptionsProvider_PlaceOptionsOrderAsync_ThrowsNotSupported()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Webull);
        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.PlaceOptionsOrderAsync("cred", new() { Symbol = "AAPL" }));
    }

    [Fact]
    public async Task NonOptionsProvider_GetOptionsChainAsync_ThrowsNotSupported()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Webull);
        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.GetOptionsChainAsync("cred", "AAPL"));
    }
}
