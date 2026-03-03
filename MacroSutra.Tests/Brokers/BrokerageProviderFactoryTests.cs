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
        services.AddHttpClient("Webull");
        services.AddSingleton<WebullBrokerageProvider>();
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
    public void GetProvider_InteractiveBrokers_ThrowsNotSupported()
    {
        var factory = CreateFactory();
        Assert.Throws<NotSupportedException>(() => factory.GetProvider(BrokerageProvider.InteractiveBrokers));
    }

    [Fact]
    public void GetProvider_TDAmeritrade_ThrowsNotSupported()
    {
        var factory = CreateFactory();
        Assert.Throws<NotSupportedException>(() => factory.GetProvider(BrokerageProvider.TDAmeritrade));
    }
}
