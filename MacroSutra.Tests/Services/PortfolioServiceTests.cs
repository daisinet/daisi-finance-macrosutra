using MacroSutra.Brokers;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MacroSutra.Tests.Services;

public class PortfolioServiceTests
{
    private static (PortfolioService service, Mock<MacroSutraCosmo> cosmo) CreateSut()
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var services = new ServiceCollection();
        services.AddSingleton<PaperBrokerageProvider>();
        var sp = services.BuildServiceProvider();
        var factory = new BrokerageProviderFactory(sp);
        var service = new PortfolioService(cosmo.Object, factory);
        return (service, cosmo);
    }

    [Fact]
    public void ValidateBrokerageAccount_EmptyName_Throws()
    {
        var account = new BrokerageAccount { Name = "" };

        var ex = Assert.Throws<InvalidOperationException>(() => PortfolioService.ValidateBrokerageAccount(account));
        Assert.Contains("name is required", ex.Message);
    }

    [Fact]
    public void ValidateBrokerageAccount_ValidName_DoesNotThrow()
    {
        var account = new BrokerageAccount { Name = "My Paper Account" };

        PortfolioService.ValidateBrokerageAccount(account); // Should not throw
    }

    [Fact]
    public async Task CreateBrokerageAccountAsync_Validates_ThenDelegates()
    {
        var (service, cosmo) = CreateSut();
        var account = new BrokerageAccount { Name = "Paper", AccountId = "acc1" };
        cosmo.Setup(c => c.CreateBrokerageAccountAsync(account)).ReturnsAsync(account);

        var result = await service.CreateBrokerageAccountAsync(account);

        cosmo.Verify(c => c.CreateBrokerageAccountAsync(account), Times.Once);
    }

    [Fact]
    public async Task CreateBrokerageAccountAsync_InvalidName_Throws()
    {
        var (service, _) = CreateSut();
        var account = new BrokerageAccount { Name = "  " };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBrokerageAccountAsync(account));
    }

    [Fact]
    public async Task DeactivateBrokerageAccountAsync_SetsIsActiveFalse()
    {
        var (service, cosmo) = CreateSut();
        var account = new BrokerageAccount { id = "a1", AccountId = "acc1", IsActive = true };

        cosmo.Setup(c => c.GetBrokerageAccountAsync("a1", "acc1")).ReturnsAsync(account);
        cosmo.Setup(c => c.UpdateBrokerageAccountAsync(It.IsAny<BrokerageAccount>()))
             .ReturnsAsync((BrokerageAccount a) => a);

        var result = await service.DeactivateBrokerageAccountAsync("a1", "acc1");

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task DeactivateBrokerageAccountAsync_NotFound_Throws()
    {
        var (service, cosmo) = CreateSut();
        cosmo.Setup(c => c.GetBrokerageAccountAsync("a1", "acc1")).ReturnsAsync((BrokerageAccount?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeactivateBrokerageAccountAsync("a1", "acc1"));

        Assert.Contains("not found", ex.Message);
    }
}
