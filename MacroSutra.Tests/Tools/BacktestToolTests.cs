using MacroSutra.Core.Models;
using MacroSutra.Services;
using MacroSutra.Tools;
using Moq;
using Daisi.SDK.Interfaces.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tests.Tools;

public class BacktestToolTests
{
    private readonly Mock<BacktestService> _backtestService = new(MockBehavior.Strict, null!, null!, null!, null!);

    private IToolContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_backtestService.Object);
        var sp = services.BuildServiceProvider();
        var ctx = new Mock<IToolContext>();
        ctx.Setup(c => c.Services).Returns(sp);
        return ctx.Object;
    }

    // ── Metadata ──

    [Fact]
    public void Id_IsCorrect() => Assert.Equal("macrosutra-backtest", new BacktestTool().Id);

    // ── Run validation ──

    [Fact]
    public async Task Run_MissingStrategyId_ReturnsError()
    {
        var result = await BacktestTool.ExecuteAsync(CreateContext(), "run", "acct1", null, "AAPL", "2024-01-01", "2024-12-31");
        Assert.False(result.Success);
        Assert.Contains("strategyId is required", result.ErrorMessage);
    }

    [Fact]
    public async Task Run_MissingSymbol_ReturnsError()
    {
        var result = await BacktestTool.ExecuteAsync(CreateContext(), "run", "acct1", "s1", null, "2024-01-01", "2024-12-31");
        Assert.False(result.Success);
        Assert.Contains("symbol is required", result.ErrorMessage);
    }

    [Fact]
    public async Task Run_InvalidFromDate_ReturnsError()
    {
        var result = await BacktestTool.ExecuteAsync(CreateContext(), "run", "acct1", "s1", "AAPL", "bad-date", "2024-12-31");
        Assert.False(result.Success);
        Assert.Contains("fromDate", result.ErrorMessage);
    }

    [Fact]
    public async Task Run_InvalidToDate_ReturnsError()
    {
        var result = await BacktestTool.ExecuteAsync(CreateContext(), "run", "acct1", "s1", "AAPL", "2024-01-01", "not-a-date");
        Assert.False(result.Success);
        Assert.Contains("toDate", result.ErrorMessage);
    }

    // ── List ──

    [Fact]
    public async Task List_ReturnsBacktests()
    {
        _backtestService.Setup(s => s.GetBacktestsAsync("acct1", "s1")).ReturnsAsync(new List<BacktestResult>
        {
            new() { id = "bt1", StrategyName = "Test", Symbol = "AAPL" }
        });

        var result = await BacktestTool.ExecuteAsync(CreateContext(), "list", "acct1", "s1", null, null, null);
        Assert.True(result.Success);
        Assert.Contains("1 backtest", result.OutputMessage);
    }

    // ── Get ──

    [Fact]
    public async Task Get_MissingId_ReturnsError()
    {
        var result = await BacktestTool.ExecuteAsync(CreateContext(), "get", "acct1", null, null, null, null);
        Assert.False(result.Success);
        Assert.Contains("backtest ID", result.ErrorMessage);
    }

    [Fact]
    public async Task Get_NotFound_ReturnsError()
    {
        _backtestService.Setup(s => s.GetBacktestAsync("bt999", "acct1")).ReturnsAsync((BacktestResult?)null);

        var result = await BacktestTool.ExecuteAsync(CreateContext(), "get", "acct1", "bt999", null, null, null);
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    // ── Unknown action ──

    [Fact]
    public async Task UnknownAction_ReturnsError()
    {
        var result = await BacktestTool.ExecuteAsync(CreateContext(), "invalid", "acct1", null, null, null, null);
        Assert.False(result.Success);
        Assert.Contains("Unknown action", result.ErrorMessage);
    }
}
