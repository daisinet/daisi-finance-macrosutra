using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;
using MacroSutra.Tools;
using Moq;
using Daisi.SDK.Interfaces.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace MacroSutra.Tests.Tools;

public class TradeToolTests
{
    // ── Metadata ──

    [Fact]
    public void Id_IsCorrect() => Assert.Equal("macrosutra-trade", new TradeTool().Id);

    [Fact]
    public void Parameters_HasSixEntries() => Assert.Equal(6, new TradeTool().Parameters.Length);

    // ── Buy validation ──

    [Fact]
    public async Task Buy_MissingSymbol_ReturnsError()
    {
        var ctx = CreateMinimalContext();
        var result = await TradeTool.ExecuteAsync(ctx, "buy", "acct1", null, "10", null);
        Assert.False(result.Success);
        Assert.Contains("symbol is required", result.ErrorMessage);
    }

    [Fact]
    public async Task Buy_InvalidQuantity_ReturnsError()
    {
        var ctx = CreateMinimalContext();
        var result = await TradeTool.ExecuteAsync(ctx, "buy", "acct1", "AAPL", "abc", null);
        Assert.False(result.Success);
        Assert.Contains("quantity must be a positive number", result.ErrorMessage);
    }

    [Fact]
    public async Task Buy_ZeroQuantity_ReturnsError()
    {
        var ctx = CreateMinimalContext();
        var result = await TradeTool.ExecuteAsync(ctx, "buy", "acct1", "AAPL", "0", null);
        Assert.False(result.Success);
        Assert.Contains("quantity must be a positive number", result.ErrorMessage);
    }

    // ── Status validation ──

    [Fact]
    public async Task Status_MissingOrderId_ReturnsError()
    {
        var ctx = CreateMinimalContext();
        var result = await TradeTool.ExecuteAsync(ctx, "status", "acct1", null, null, null);
        Assert.False(result.Success);
        Assert.Contains("orderId is required", result.ErrorMessage);
    }

    [Fact]
    public async Task Status_NotFound_ReturnsError()
    {
        var tradeService = new Mock<TradeService>(MockBehavior.Strict, null!);
        tradeService.Setup(s => s.GetTradeAsync("t999", "acct1")).ReturnsAsync((Trade?)null);

        var ctx = CreateContextWithTradeService(tradeService.Object);
        var result = await TradeTool.ExecuteAsync(ctx, "status", "acct1", null, null, "t999");
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    // ── History ──

    [Fact]
    public async Task History_ReturnsTrades()
    {
        var tradeService = new Mock<TradeService>(MockBehavior.Strict, null!);
        tradeService.Setup(s => s.GetTradesAsync("acct1", null, null, null)).ReturnsAsync(new List<Trade>
        {
            new() { id = "t1", Symbol = "AAPL", Side = TradeSide.Buy, Status = TradeStatus.Filled }
        });

        var ctx = CreateContextWithTradeService(tradeService.Object);
        var result = await TradeTool.ExecuteAsync(ctx, "history", "acct1", null, null, null);
        Assert.True(result.Success);
        Assert.Contains("AAPL", result.Output);
    }

    // ── Unknown action ──

    [Fact]
    public async Task UnknownAction_ReturnsError()
    {
        var ctx = CreateMinimalContext();
        var result = await TradeTool.ExecuteAsync(ctx, "bogus", "acct1", null, null, null);
        Assert.False(result.Success);
        Assert.Contains("Unknown action", result.ErrorMessage);
    }

    // ── Helpers ──

    private static IToolContext CreateMinimalContext()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var ctx = new Mock<IToolContext>();
        ctx.Setup(c => c.Services).Returns(sp);
        return ctx.Object;
    }

    private static IToolContext CreateContextWithTradeService(TradeService tradeService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(tradeService);
        var sp = services.BuildServiceProvider();
        var ctx = new Mock<IToolContext>();
        ctx.Setup(c => c.Services).Returns(sp);
        return ctx.Object;
    }
}
