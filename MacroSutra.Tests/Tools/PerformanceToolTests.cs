using MacroSutra.Core.Models;
using MacroSutra.Services;
using MacroSutra.Tools;
using Moq;
using Daisi.SDK.Interfaces.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tests.Tools;

public class PerformanceToolTests
{
    private readonly Mock<StrategyPerformanceService> _perfService = new(MockBehavior.Strict, null!);

    private IToolContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_perfService.Object);
        var sp = services.BuildServiceProvider();
        var ctx = new Mock<IToolContext>();
        ctx.Setup(c => c.Services).Returns(sp);
        return ctx.Object;
    }

    // ── Metadata ──

    [Fact]
    public void Id_IsCorrect() => Assert.Equal("macrosutra-performance", new PerformanceTool().Id);

    // ── Summary ──

    [Fact]
    public async Task Summary_ReturnsSummary()
    {
        _perfService.Setup(s => s.GetPerformanceSummaryAsync("acct1", "s1")).ReturnsAsync(new StrategyPerformanceSummary
        {
            TotalTriggers = 25,
            WinRate = 0.68m,
            TotalPnL = 5200m
        });

        var result = await PerformanceTool.ExecuteAsync(CreateContext(), "summary", "acct1", "s1");
        Assert.True(result.Success);
        Assert.Contains("25", result.OutputMessage);
    }

    // ── Triggers ──

    [Fact]
    public async Task Triggers_ReturnsTriggerHistory()
    {
        _perfService.Setup(s => s.GetTriggerHistoryAsync("acct1", "s1")).ReturnsAsync(new List<StrategyTriggerRecord>
        {
            new() { StrategyId = "s1", Symbol = "AAPL", TriggeredUtc = DateTime.UtcNow }
        });

        var result = await PerformanceTool.ExecuteAsync(CreateContext(), "triggers", "acct1", "s1");
        Assert.True(result.Success);
        Assert.Contains("1 trigger", result.OutputMessage);
    }

    // ── Unknown action ──

    [Fact]
    public async Task UnknownAction_ReturnsError()
    {
        var result = await PerformanceTool.ExecuteAsync(CreateContext(), "nope", "acct1", "s1");
        Assert.False(result.Success);
        Assert.Contains("Unknown action", result.ErrorMessage);
    }
}
