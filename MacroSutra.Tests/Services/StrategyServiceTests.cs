using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace MacroSutra.Tests.Services;

public class StrategyServiceTests
{
    private static (StrategyService service, Mock<MacroSutraCosmo> cosmo) CreateSut()
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var service = new StrategyService(cosmo.Object);
        return (service, cosmo);
    }

    [Fact]
    public void ValidateStrategy_EmptyName_Throws()
    {
        var strategy = new TradingStrategy { Name = "", Symbols = new List<string> { "AAPL" } };

        var ex = Assert.Throws<InvalidOperationException>(() => StrategyService.ValidateStrategy(strategy));
        Assert.Contains("name is required", ex.Message);
    }

    [Fact]
    public void ValidateStrategy_NoSymbols_Throws()
    {
        var strategy = new TradingStrategy { Name = "Test", Symbols = new List<string>() };

        var ex = Assert.Throws<InvalidOperationException>(() => StrategyService.ValidateStrategy(strategy));
        Assert.Contains("symbol is required", ex.Message);
    }

    [Fact]
    public void ValidateStrategy_ValidInput_DoesNotThrow()
    {
        var strategy = new TradingStrategy { Name = "Bull Run", Symbols = new List<string> { "AAPL", "MSFT" } };

        StrategyService.ValidateStrategy(strategy); // Should not throw
    }

    [Fact]
    public async Task CreateStrategyAsync_Validates_ThenDelegates()
    {
        var (service, cosmo) = CreateSut();
        var strategy = new TradingStrategy { Name = "Test", Symbols = new List<string> { "SPY" }, AccountId = "acc1" };
        cosmo.Setup(c => c.CreateStrategyAsync(strategy)).ReturnsAsync(strategy);

        var result = await service.CreateStrategyAsync(strategy);

        cosmo.Verify(c => c.CreateStrategyAsync(strategy), Times.Once);
    }

    [Fact]
    public async Task CreateStrategyAsync_InvalidName_Throws()
    {
        var (service, _) = CreateSut();
        var strategy = new TradingStrategy { Name = "", Symbols = new List<string> { "SPY" } };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateStrategyAsync(strategy));
    }

    [Fact]
    public async Task ActivateStrategyAsync_SetsIsActiveTrue()
    {
        var (service, cosmo) = CreateSut();
        var strategy = new TradingStrategy { id = "s1", AccountId = "acc1", IsActive = false };

        cosmo.Setup(c => c.GetStrategyAsync("s1", "acc1")).ReturnsAsync(strategy);
        cosmo.Setup(c => c.UpdateStrategyAsync(It.IsAny<TradingStrategy>()))
             .ReturnsAsync((TradingStrategy s) => s);

        var result = await service.ActivateStrategyAsync("s1", "acc1");

        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task DeactivateStrategyAsync_SetsIsActiveFalse()
    {
        var (service, cosmo) = CreateSut();
        var strategy = new TradingStrategy { id = "s1", AccountId = "acc1", IsActive = true };

        cosmo.Setup(c => c.GetStrategyAsync("s1", "acc1")).ReturnsAsync(strategy);
        cosmo.Setup(c => c.UpdateStrategyAsync(It.IsAny<TradingStrategy>()))
             .ReturnsAsync((TradingStrategy s) => s);

        var result = await service.DeactivateStrategyAsync("s1", "acc1");

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task ActivateStrategyAsync_NotFound_Throws()
    {
        var (service, cosmo) = CreateSut();
        cosmo.Setup(c => c.GetStrategyAsync("s1", "acc1")).ReturnsAsync((TradingStrategy?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ActivateStrategyAsync("s1", "acc1"));

        Assert.Contains("not found", ex.Message);
    }
}
