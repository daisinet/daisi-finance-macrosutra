using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace MacroSutra.Tests.Services;

public class UserManagementServiceTests
{
    private static (UserManagementService service, Mock<MacroSutraCosmo> cosmo) CreateSut()
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var service = new UserManagementService(cosmo.Object);
        return (service, cosmo);
    }

    [Fact]
    public async Task CreateUserAsync_DelegatesToCosmo()
    {
        var (service, cosmo) = CreateSut();
        var user = new MacroSutraUser { Name = "Alice", AccountId = "acc1" };
        cosmo.Setup(c => c.CreateUserAsync(user)).ReturnsAsync(user);

        var result = await service.CreateUserAsync(user);

        Assert.Equal("Alice", result.Name);
        cosmo.Verify(c => c.CreateUserAsync(user), Times.Once);
    }

    [Fact]
    public async Task DeactivateUserAsync_BlocksLastOwner()
    {
        var (service, cosmo) = CreateSut();
        var owner = new MacroSutraUser { id = "u1", AccountId = "acc1", Role = MacroSutraRole.Owner, IsActive = true };

        cosmo.Setup(c => c.GetUserAsync("u1", "acc1")).ReturnsAsync(owner);
        cosmo.Setup(c => c.GetUsersAsync("acc1", true)).ReturnsAsync(new List<MacroSutraUser> { owner });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeactivateUserAsync("u1", "acc1"));

        Assert.Contains("last active Owner", ex.Message);
    }

    [Fact]
    public async Task DeactivateUserAsync_AllowsWhenMultipleOwners()
    {
        var (service, cosmo) = CreateSut();
        var owner1 = new MacroSutraUser { id = "u1", AccountId = "acc1", Role = MacroSutraRole.Owner, IsActive = true };
        var owner2 = new MacroSutraUser { id = "u2", AccountId = "acc1", Role = MacroSutraRole.Owner, IsActive = true };

        cosmo.Setup(c => c.GetUserAsync("u1", "acc1")).ReturnsAsync(owner1);
        cosmo.Setup(c => c.GetUsersAsync("acc1", true)).ReturnsAsync(new List<MacroSutraUser> { owner1, owner2 });
        cosmo.Setup(c => c.UpdateUserAsync(It.IsAny<MacroSutraUser>()))
             .ReturnsAsync((MacroSutraUser u) => u);

        var result = await service.DeactivateUserAsync("u1", "acc1");

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task DeactivateUserAsync_AllowsNonOwner()
    {
        var (service, cosmo) = CreateSut();
        var trader = new MacroSutraUser { id = "u1", AccountId = "acc1", Role = MacroSutraRole.Trader, IsActive = true };

        cosmo.Setup(c => c.GetUserAsync("u1", "acc1")).ReturnsAsync(trader);
        cosmo.Setup(c => c.UpdateUserAsync(It.IsAny<MacroSutraUser>()))
             .ReturnsAsync((MacroSutraUser u) => u);

        var result = await service.DeactivateUserAsync("u1", "acc1");

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task DeactivateUserAsync_UserNotFound_Throws()
    {
        var (service, cosmo) = CreateSut();
        cosmo.Setup(c => c.GetUserAsync("u1", "acc1")).ReturnsAsync((MacroSutraUser?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeactivateUserAsync("u1", "acc1"));

        Assert.Contains("User not found", ex.Message);
    }

    [Fact]
    public async Task ReactivateUserAsync_SetsIsActiveTrue()
    {
        var (service, cosmo) = CreateSut();
        var user = new MacroSutraUser { id = "u1", AccountId = "acc1", IsActive = false };

        cosmo.Setup(c => c.GetUserAsync("u1", "acc1")).ReturnsAsync(user);
        cosmo.Setup(c => c.UpdateUserAsync(It.IsAny<MacroSutraUser>()))
             .ReturnsAsync((MacroSutraUser u) => u);

        var result = await service.ReactivateUserAsync("u1", "acc1");

        Assert.True(result.IsActive);
    }
}
