using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tests.Brokers;

public class BrokerageProviderFactoryTests
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

    [Fact]
    public void GetProvider_Paper_ReturnsPaperProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Paper);
        Assert.IsType<PaperBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_Alpaca_ReturnsAlpacaProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Alpaca);
        Assert.IsType<AlpacaBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_Webull_ReturnsWebullProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Webull);
        Assert.IsType<WebullBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_Schwab_ReturnsSchwabProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Schwab);
        Assert.IsType<SchwabBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_Tradier_ReturnsTradierProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Tradier);
        Assert.IsType<TradierBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_Tastytrade_ReturnsTastytradeProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Tastytrade);
        Assert.IsType<TastytradeBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_TradeStation_ReturnsTradeStationProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.TradeStation);
        Assert.IsType<TradeStationBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_PublicCom_ReturnsPublicComProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.PublicCom);
        Assert.IsType<PublicComBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_InteractiveBrokers_ReturnsIBKRProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.InteractiveBrokers);
        Assert.IsType<InteractiveBrokersBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_Moomoo_ReturnsMoomooProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Moomoo);
        Assert.IsType<MoomooBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_Robinhood_ReturnsRobinhoodProvider()
    {
        var factory = CreateFactory();
        var provider = factory.GetProvider(BrokerageProvider.Robinhood);
        Assert.IsType<RobinhoodBrokerageProvider>(provider);
    }

    [Fact]
    public void GetProvider_TDAmeritrade_ThrowsNotSupported()
    {
        var factory = CreateFactory();
        Assert.Throws<NotSupportedException>(() => factory.GetProvider(BrokerageProvider.TDAmeritrade));
    }
}
