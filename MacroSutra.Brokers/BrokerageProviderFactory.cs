using MacroSutra.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Brokers;

/// <summary>
/// Resolves the correct IBrokerageProvider by enum value from DI.
/// </summary>
public class BrokerageProviderFactory(IServiceProvider serviceProvider)
{
    public virtual IBrokerageProvider GetProvider(BrokerageProvider provider)
    {
        return provider switch
        {
            Core.Enums.BrokerageProvider.Paper => serviceProvider.GetRequiredService<PaperBrokerageProvider>(),
            Core.Enums.BrokerageProvider.Alpaca => serviceProvider.GetRequiredService<AlpacaBrokerageProvider>(),
            Core.Enums.BrokerageProvider.Webull => serviceProvider.GetRequiredService<WebullBrokerageProvider>(),
            Core.Enums.BrokerageProvider.Schwab => serviceProvider.GetRequiredService<SchwabBrokerageProvider>(),
            Core.Enums.BrokerageProvider.Tradier => serviceProvider.GetRequiredService<TradierBrokerageProvider>(),
            Core.Enums.BrokerageProvider.Tastytrade => serviceProvider.GetRequiredService<TastytradeBrokerageProvider>(),
            Core.Enums.BrokerageProvider.TradeStation => serviceProvider.GetRequiredService<TradeStationBrokerageProvider>(),
            Core.Enums.BrokerageProvider.PublicCom => serviceProvider.GetRequiredService<PublicComBrokerageProvider>(),
            Core.Enums.BrokerageProvider.InteractiveBrokers => serviceProvider.GetRequiredService<InteractiveBrokersBrokerageProvider>(),
            Core.Enums.BrokerageProvider.Moomoo => serviceProvider.GetRequiredService<MoomooBrokerageProvider>(),
            Core.Enums.BrokerageProvider.Robinhood => serviceProvider.GetRequiredService<RobinhoodBrokerageProvider>(),
            _ => throw new NotSupportedException($"Brokerage provider '{provider}' is not yet supported.")
        };
    }
}
