using MacroSutra.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Brokers;

/// <summary>
/// Resolves the correct IBrokerageProvider by enum value from DI.
/// </summary>
public class BrokerageProviderFactory(IServiceProvider serviceProvider)
{
    public IBrokerageProvider GetProvider(BrokerageProvider provider)
    {
        return provider switch
        {
            Core.Enums.BrokerageProvider.Paper => serviceProvider.GetRequiredService<PaperBrokerageProvider>(),
            _ => throw new NotSupportedException($"Brokerage provider '{provider}' is not yet supported.")
        };
    }
}
