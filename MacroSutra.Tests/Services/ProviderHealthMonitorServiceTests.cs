using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace MacroSutra.Tests.Services;

public class ProviderHealthMonitorServiceTests
{
    private static ProviderHealthMonitorService CreateService()
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
        var factory = new BrokerageProviderFactory(sp);

        return new ProviderHealthMonitorService(
            factory,
            Mock.Of<ILogger<ProviderHealthMonitorService>>());
    }

    [Fact]
    public void IsHealthy_UncheckedProvider_ReturnsTrue()
    {
        var service = CreateService();
        Assert.True(service.IsHealthy(BrokerageProvider.Alpaca));
    }

    [Fact]
    public void IsHealthy_PaperProvider_AlwaysTrue()
    {
        var service = CreateService();
        Assert.True(service.IsHealthy(BrokerageProvider.Paper));
    }

    [Fact]
    public void GetHealth_UncheckedProvider_ReturnsNull()
    {
        var service = CreateService();
        Assert.Null(service.GetHealth(BrokerageProvider.Alpaca));
    }

    [Fact]
    public async Task CheckProviderAsync_SuccessfulCheck_MarksHealthy()
    {
        var service = CreateService();

        await service.CheckProviderAsync(BrokerageProvider.InteractiveBrokers);

        var health = service.GetHealth(BrokerageProvider.InteractiveBrokers);
        Assert.NotNull(health);
        Assert.True(health!.IsHealthy);
        Assert.Equal(0, health.ConsecutiveFailures);
        Assert.Null(health.ErrorMessage);
    }

    [Fact]
    public async Task CheckProviderAsync_Records_Latency()
    {
        var service = CreateService();

        await service.CheckProviderAsync(BrokerageProvider.InteractiveBrokers);

        var health = service.GetHealth(BrokerageProvider.InteractiveBrokers);
        Assert.NotNull(health);
        Assert.True(health!.LatencyMs >= 0);
        Assert.True(health.LastCheckUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CheckProviderAsync_ResetsConsecutiveFailures()
    {
        var service = CreateService();

        await service.CheckProviderAsync(BrokerageProvider.Moomoo);

        var health = service.GetHealth(BrokerageProvider.Moomoo);
        Assert.NotNull(health);
        Assert.Equal(0, health!.ConsecutiveFailures);
    }

    [Fact]
    public void GetAllHealth_ReturnsReadOnlyDictionary()
    {
        var service = CreateService();

        var all = service.GetAllHealth();
        Assert.NotNull(all);
        Assert.IsAssignableFrom<IReadOnlyDictionary<BrokerageProvider, ProviderHealthStatus>>(all);
    }

    [Fact]
    public async Task CheckProviderAsync_MultipleSuccessful_StaysHealthy()
    {
        var service = CreateService();

        await service.CheckProviderAsync(BrokerageProvider.InteractiveBrokers);
        await service.CheckProviderAsync(BrokerageProvider.InteractiveBrokers);
        await service.CheckProviderAsync(BrokerageProvider.InteractiveBrokers);

        var health = service.GetHealth(BrokerageProvider.InteractiveBrokers);
        Assert.NotNull(health);
        Assert.True(health!.IsHealthy);
        Assert.Equal(0, health.ConsecutiveFailures);
    }
}
