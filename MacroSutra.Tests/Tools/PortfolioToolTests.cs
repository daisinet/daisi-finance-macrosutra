using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;
using MacroSutra.Tools;
using Moq;
using Daisi.SDK.Interfaces.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tests.Tools;

public class PortfolioToolTests
{
    private readonly Mock<PortfolioService> _portfolioService = new(MockBehavior.Strict, null!, null!);

    private IToolContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_portfolioService.Object);
        var sp = services.BuildServiceProvider();
        var ctx = new Mock<IToolContext>();
        ctx.Setup(c => c.Services).Returns(sp);
        return ctx.Object;
    }

    // ── Metadata ──

    [Fact]
    public void Id_IsCorrect() => Assert.Equal("macrosutra-portfolio", new PortfolioTool().Id);

    [Fact]
    public void Name_IsCorrect() => Assert.Equal("MacroSutra Portfolio", new PortfolioTool().Name);

    [Fact]
    public void Parameters_HasThreeEntries() => Assert.Equal(3, new PortfolioTool().Parameters.Length);

    // ── Positions ──

    [Fact]
    public async Task Positions_ReturnsPositions()
    {
        var positions = new List<Position>
        {
            new() { Symbol = "AAPL", Quantity = 10, AverageCost = 150, CurrentPrice = 160 }
        };
        _portfolioService.Setup(s => s.GetPositionsAsync("acct1", null)).ReturnsAsync(positions);

        var result = await PortfolioTool.ExecuteAsync(CreateContext(), "positions", "acct1", null);
        Assert.True(result.Success);
        Assert.Contains("AAPL", result.Output);
    }

    // ── Balance ──

    [Fact]
    public async Task Balance_ReturnsAccounts()
    {
        var accounts = new List<BrokerageAccount>
        {
            new() { Name = "Paper", Provider = BrokerageProvider.Paper, CachedBalance = 100_000, IsActive = true }
        };
        _portfolioService.Setup(s => s.GetBrokerageAccountsAsync("acct1", true)).ReturnsAsync(accounts);

        var result = await PortfolioTool.ExecuteAsync(CreateContext(), "balance", "acct1", null);
        Assert.True(result.Success);
        Assert.Contains("Paper", result.Output);
    }

    // ── Accounts ──

    [Fact]
    public async Task Accounts_ReturnsAll()
    {
        var accounts = new List<BrokerageAccount>
        {
            new() { id = "ba1", Name = "My Alpaca", Provider = BrokerageProvider.Alpaca, IsActive = true }
        };
        _portfolioService.Setup(s => s.GetBrokerageAccountsAsync("acct1", false)).ReturnsAsync(accounts);

        var result = await PortfolioTool.ExecuteAsync(CreateContext(), "accounts", "acct1", null);
        Assert.True(result.Success);
        Assert.Contains("ba1", result.Output);
    }

    // ── Unknown action ──

    [Fact]
    public async Task UnknownAction_ReturnsError()
    {
        var result = await PortfolioTool.ExecuteAsync(CreateContext(), "bogus", "acct1", null);
        Assert.False(result.Success);
        Assert.Contains("Unknown action", result.ErrorMessage);
    }
}
