using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MacroSutra.Tests.Services;

public class PositionSyncServiceTests
{
    private static (PositionSyncService service, Mock<MacroSutraCosmo> cosmo, Mock<IBrokerageProvider> provider) CreateSut(BrokerageProvider brokerType = BrokerageProvider.Alpaca)
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var mockProvider = new Mock<IBrokerageProvider>();

        // Wire the factory to return the mock provider for the specified broker type
        var services = new ServiceCollection();
        services.AddSingleton<PaperBrokerageProvider>();
        services.AddSingleton(mockProvider.Object);
        var sp = services.BuildServiceProvider();

        var factoryMock = new Mock<BrokerageProviderFactory>(sp);
        factoryMock.Setup(f => f.GetProvider(brokerType)).Returns(mockProvider.Object);
        factoryMock.Setup(f => f.GetProvider(BrokerageProvider.Paper)).Returns(sp.GetRequiredService<PaperBrokerageProvider>());

        var service = new PositionSyncService(cosmo.Object, factoryMock.Object);
        return (service, cosmo, mockProvider);
    }

    [Fact]
    public async Task SyncAccountAsync_UpsertsNewPositions()
    {
        var (service, cosmo, provider) = CreateSut();
        var account = new BrokerageAccount { id = "bra-1", AccountId = "acc1", Provider = BrokerageProvider.Alpaca, CredentialData = "{}" };

        provider.Setup(p => p.TryRefreshCredentialsAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        provider.Setup(p => p.GetPositionsAsync(It.IsAny<string>())).ReturnsAsync(new List<Position>
        {
            new() { Symbol = "AAPL", Quantity = 10, AverageCost = 150, CurrentPrice = 155 }
        });
        provider.Setup(p => p.GetAccountBalanceAsync(It.IsAny<string>())).ReturnsAsync(50_000m);

        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(new List<Position>());
        cosmo.Setup(c => c.CreatePositionAsync(It.IsAny<Position>())).ReturnsAsync((Position p) => p);
        cosmo.Setup(c => c.UpdateBrokerageAccountAsync(It.IsAny<BrokerageAccount>())).ReturnsAsync((BrokerageAccount a) => a);

        var result = await service.SyncAccountAsync(account);

        Assert.Equal(1, result.PositionCount);
        Assert.Equal(50_000m, result.Balance);
        Assert.Null(result.Error);
        cosmo.Verify(c => c.CreatePositionAsync(It.Is<Position>(p => p.Symbol == "AAPL")), Times.Once);
    }

    [Fact]
    public async Task SyncAccountAsync_UpdatesExistingPositions()
    {
        var (service, cosmo, provider) = CreateSut();
        var account = new BrokerageAccount { id = "bra-1", AccountId = "acc1", Provider = BrokerageProvider.Alpaca, CredentialData = "{}" };

        provider.Setup(p => p.TryRefreshCredentialsAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        provider.Setup(p => p.GetPositionsAsync(It.IsAny<string>())).ReturnsAsync(new List<Position>
        {
            new() { Symbol = "AAPL", Quantity = 20, AverageCost = 150, CurrentPrice = 160 }
        });
        provider.Setup(p => p.GetAccountBalanceAsync(It.IsAny<string>())).ReturnsAsync(40_000m);

        var existingPosition = new Position { id = "pos-1", AccountId = "acc1", BrokerageAccountId = "bra-1", Symbol = "AAPL", Quantity = 10 };
        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(new List<Position> { existingPosition });
        cosmo.Setup(c => c.UpdatePositionAsync(It.IsAny<Position>())).ReturnsAsync((Position p) => p);
        cosmo.Setup(c => c.UpdateBrokerageAccountAsync(It.IsAny<BrokerageAccount>())).ReturnsAsync((BrokerageAccount a) => a);

        var result = await service.SyncAccountAsync(account);

        cosmo.Verify(c => c.UpdatePositionAsync(It.Is<Position>(p => p.Quantity == 20)), Times.Once);
        cosmo.Verify(c => c.CreatePositionAsync(It.IsAny<Position>()), Times.Never);
    }

    [Fact]
    public async Task SyncAccountAsync_DeletesStalePositions()
    {
        var (service, cosmo, provider) = CreateSut();
        var account = new BrokerageAccount { id = "bra-1", AccountId = "acc1", Provider = BrokerageProvider.Alpaca, CredentialData = "{}" };

        provider.Setup(p => p.TryRefreshCredentialsAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        provider.Setup(p => p.GetPositionsAsync(It.IsAny<string>())).ReturnsAsync(new List<Position>()); // Empty remote
        provider.Setup(p => p.GetAccountBalanceAsync(It.IsAny<string>())).ReturnsAsync(100_000m);

        var stalePosition = new Position { id = "pos-old", AccountId = "acc1", BrokerageAccountId = "bra-1", Symbol = "TSLA" };
        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(new List<Position> { stalePosition });
        cosmo.Setup(c => c.DeletePositionAsync("pos-old", "acc1")).Returns(Task.CompletedTask);
        cosmo.Setup(c => c.UpdateBrokerageAccountAsync(It.IsAny<BrokerageAccount>())).ReturnsAsync((BrokerageAccount a) => a);

        var result = await service.SyncAccountAsync(account);

        cosmo.Verify(c => c.DeletePositionAsync("pos-old", "acc1"), Times.Once);
    }

    [Fact]
    public async Task SyncAccountAsync_RefreshesTokenWhenNeeded()
    {
        var (service, cosmo, provider) = CreateSut();
        var account = new BrokerageAccount { id = "bra-1", AccountId = "acc1", Provider = BrokerageProvider.Alpaca, CredentialData = """{"old":"creds"}""" };

        var refreshedCreds = """{"new":"creds"}""";
        provider.Setup(p => p.TryRefreshCredentialsAsync(It.IsAny<string>())).ReturnsAsync(refreshedCreds);
        provider.Setup(p => p.GetPositionsAsync(refreshedCreds)).ReturnsAsync(new List<Position>());
        provider.Setup(p => p.GetAccountBalanceAsync(refreshedCreds)).ReturnsAsync(10_000m);

        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-1")).ReturnsAsync(new List<Position>());
        cosmo.Setup(c => c.UpdateBrokerageAccountAsync(It.IsAny<BrokerageAccount>())).ReturnsAsync((BrokerageAccount a) => a);

        await service.SyncAccountAsync(account);

        // Should have updated account with refreshed credentials
        cosmo.Verify(c => c.UpdateBrokerageAccountAsync(It.Is<BrokerageAccount>(a => a.CredentialData == refreshedCreds)), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SyncAccountAsync_Error_ReturnsErrorMessage()
    {
        var (service, cosmo, provider) = CreateSut();
        var account = new BrokerageAccount { id = "bra-1", AccountId = "acc1", Provider = BrokerageProvider.Alpaca, CredentialData = "{}" };

        provider.Setup(p => p.TryRefreshCredentialsAsync(It.IsAny<string>())).ThrowsAsync(new Exception("Connection failed"));

        var result = await service.SyncAccountAsync(account);

        Assert.NotNull(result.Error);
        Assert.Contains("Connection failed", result.Error);
    }

    [Fact]
    public async Task SyncAllAccountsAsync_SkipsPaperAccounts()
    {
        var (service, cosmo, provider) = CreateSut();

        cosmo.Setup(c => c.GetBrokerageAccountsAsync("acc1", true)).ReturnsAsync(new List<BrokerageAccount>
        {
            new() { id = "bra-paper", AccountId = "acc1", Provider = BrokerageProvider.Paper },
            new() { id = "bra-alpaca", AccountId = "acc1", Provider = BrokerageProvider.Alpaca, CredentialData = "{}" }
        });

        provider.Setup(p => p.TryRefreshCredentialsAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        provider.Setup(p => p.GetPositionsAsync(It.IsAny<string>())).ReturnsAsync(new List<Position>());
        provider.Setup(p => p.GetAccountBalanceAsync(It.IsAny<string>())).ReturnsAsync(10_000m);

        cosmo.Setup(c => c.GetPositionsAsync("acc1", "bra-alpaca")).ReturnsAsync(new List<Position>());
        cosmo.Setup(c => c.UpdateBrokerageAccountAsync(It.IsAny<BrokerageAccount>())).ReturnsAsync((BrokerageAccount a) => a);

        var results = await service.SyncAllAccountsAsync("acc1");

        Assert.Single(results); // Only Alpaca, not Paper
        Assert.True(results.ContainsKey("bra-alpaca"));
        Assert.False(results.ContainsKey("bra-paper"));
    }
}
