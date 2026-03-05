using MacroSutra.Core.Models;
using MacroSutra.Services;
using MacroSutra.Tools;
using Moq;
using Daisi.SDK.Interfaces.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tests.Tools;

public class StrategyToolTests
{
    private readonly Mock<StrategyService> _strategyService = new(MockBehavior.Strict, null!);
    private readonly Mock<StrategyEvaluationService> _evalService;
    private readonly Mock<StrategyTemplateService> _templateService = new(MockBehavior.Strict);

    public StrategyToolTests()
    {
        _evalService = new Mock<StrategyEvaluationService>(MockBehavior.Strict, null!, null!, null!, null!);
    }

    private IToolContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_strategyService.Object);
        services.AddSingleton(_evalService.Object);
        services.AddSingleton(_templateService.Object);
        var sp = services.BuildServiceProvider();
        var ctx = new Mock<IToolContext>();
        ctx.Setup(c => c.Services).Returns(sp);
        return ctx.Object;
    }

    // ── Metadata ──

    [Fact]
    public void Id_IsCorrect() => Assert.Equal("macrosutra-strategy", new StrategyTool().Id);

    // ── List ──

    [Fact]
    public async Task List_ReturnsStrategies()
    {
        var strategies = new List<TradingStrategy>
        {
            new() { id = "s1", Name = "RSI Dip Buy", IsActive = true }
        };
        _strategyService.Setup(s => s.GetStrategiesAsync("acct1", null)).ReturnsAsync(strategies);

        var result = await StrategyTool.ExecuteAsync(CreateContext(), "list", "acct1", null);
        Assert.True(result.Success);
        Assert.Contains("RSI Dip Buy", result.Output);
    }

    // ── Get ──

    [Fact]
    public async Task Get_MissingId_ReturnsError()
    {
        var result = await StrategyTool.ExecuteAsync(CreateContext(), "get", "acct1", null);
        Assert.False(result.Success);
        Assert.Contains("strategyId is required", result.ErrorMessage);
    }

    [Fact]
    public async Task Get_NotFound_ReturnsError()
    {
        _strategyService.Setup(s => s.GetStrategyAsync("s999", "acct1")).ReturnsAsync((TradingStrategy?)null);

        var result = await StrategyTool.ExecuteAsync(CreateContext(), "get", "acct1", "s999");
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    // ── Templates ──

    [Fact]
    public async Task Templates_ReturnsTemplates()
    {
        // StrategyTemplateService.GetTemplates() is non-virtual; use real instance
        var services = new ServiceCollection();
        services.AddSingleton(_strategyService.Object);
        services.AddSingleton(_evalService.Object);
        services.AddSingleton(new StrategyTemplateService());
        var sp = services.BuildServiceProvider();
        var ctx = new Mock<IToolContext>();
        ctx.Setup(c => c.Services).Returns(sp);

        var result = await StrategyTool.ExecuteAsync(ctx.Object, "templates", "acct1", null);
        Assert.True(result.Success);
        Assert.Contains("template", result.OutputMessage);
    }

    // ── Unknown action ──

    [Fact]
    public async Task UnknownAction_ReturnsError()
    {
        var result = await StrategyTool.ExecuteAsync(CreateContext(), "xyz", "acct1", null);
        Assert.False(result.Success);
        Assert.Contains("Unknown action", result.ErrorMessage);
    }
}
