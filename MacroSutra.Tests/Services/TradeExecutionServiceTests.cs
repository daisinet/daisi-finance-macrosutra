using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace MacroSutra.Tests.Services;

public class TradeExecutionServiceTests
{
    private static (TradeExecutionService service, Mock<MacroSutraCosmo> cosmo, Mock<BrokerageProviderFactory> factory) CreateSut()
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var factory = new Mock<BrokerageProviderFactory>(Mock.Of<IServiceProvider>());
        var portfolioService = new PortfolioService(cosmo.Object, factory.Object);
        var tradeService = new TradeService(cosmo.Object);
        var logger = Mock.Of<ILogger<TradeExecutionService>>();

        // Setup default cosmo behaviors
        cosmo.Setup(c => c.CreateTradeAsync(It.IsAny<Trade>()))
             .ReturnsAsync((Trade t) => { t.id = "trd-test-123"; return t; });
        cosmo.Setup(c => c.UpdateTradeAsync(It.IsAny<Trade>()))
             .ReturnsAsync((Trade t) => t);
        cosmo.Setup(c => c.GetTradeAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync((string id, string aid) => new Trade { id = id, AccountId = aid, Status = TradeStatus.Pending });

        var healthMonitor = new ProviderHealthMonitorService(
            factory.Object,
            Mock.Of<ILogger<ProviderHealthMonitorService>>());
        var service = new TradeExecutionService(factory.Object, portfolioService, tradeService, healthMonitor, logger);
        return (service, cosmo, factory);
    }

    private static MarketSnapshot MakeSnapshot(decimal price = 150m) =>
        new() { Symbol = "AAPL", Price = price, Volume = 1_000_000, DailyChangePercent = 1.5m };

    // ── Quantity Resolution ──

    [Fact]
    public void ResolveQuantity_Shares_ReturnsLiteralQuantity()
    {
        var action = new TradeAction { QuantityType = QuantityType.Shares, Quantity = 10 };
        var qty = TradeExecutionService.ResolveQuantity(action, 150m, 100_000m);
        Assert.Equal(10m, qty);
    }

    [Fact]
    public void ResolveQuantity_DollarAmount_DividesAmountByPrice()
    {
        var action = new TradeAction { QuantityType = QuantityType.DollarAmount, Quantity = 1500m };
        var qty = TradeExecutionService.ResolveQuantity(action, 150m, 100_000m);
        Assert.Equal(10m, qty); // 1500 / 150 = 10
    }

    [Fact]
    public void ResolveQuantity_PercentOfPortfolio_CalculatesCorrectly()
    {
        var action = new TradeAction { QuantityType = QuantityType.PercentOfPortfolio, Quantity = 10m }; // 10%
        var qty = TradeExecutionService.ResolveQuantity(action, 100m, 50_000m);
        Assert.Equal(50m, qty); // 50000 * 10% / 100 = 50
    }

    [Fact]
    public void ResolveQuantity_ZeroPrice_ReturnsZero()
    {
        var action = new TradeAction { QuantityType = QuantityType.DollarAmount, Quantity = 1000m };
        var qty = TradeExecutionService.ResolveQuantity(action, 0m, 100_000m);
        Assert.Equal(0m, qty);
    }

    // ── Alert handling ──

    [Fact]
    public async Task ExecuteActionsAsync_AlertAction_RecordsFilledTradeWithNotes()
    {
        var (service, cosmo, _) = CreateSut();

        var strategy = new TradingStrategy
        {
            id = "str-1", AccountId = "acc1", Name = "Test Alert",
            Actions = new() { new TradeAction { ActionType = TradeActionType.Alert, Side = TradeSide.Buy } }
        };

        var trades = await service.ExecuteActionsAsync(strategy, "AAPL", MakeSnapshot());

        Assert.Single(trades);
        Assert.Equal(TradeStatus.Filled, trades[0].Status);
        Assert.Contains("Alert:", trades[0].Notes);
        Assert.Contains("Test Alert", trades[0].Notes);
    }

    // ── Order placement ──

    [Fact]
    public async Task ExecuteActionsAsync_MarketOrder_PlacesOrderAndRecordsTrade()
    {
        var (service, cosmo, factory) = CreateSut();

        var brokerageAccount = new BrokerageAccount
        {
            id = "bra-1", AccountId = "acc1", Provider = BrokerageProvider.Paper,
            CredentialData = "{}", CachedBalance = 100_000m
        };

        cosmo.Setup(c => c.GetBrokerageAccountAsync("bra-1", "acc1")).ReturnsAsync(brokerageAccount);

        var mockProvider = new Mock<IBrokerageProvider>();
        mockProvider.Setup(p => p.PlaceOrderAsync(It.IsAny<string>(), It.IsAny<Trade>()))
                    .ReturnsAsync("EXT-ORDER-123");
        mockProvider.Setup(p => p.GetAccountBalanceAsync(It.IsAny<string>()))
                    .ReturnsAsync(100_000m);

        factory.Setup(f => f.GetProvider(BrokerageProvider.Paper)).Returns(mockProvider.Object);

        var strategy = new TradingStrategy
        {
            id = "str-1", AccountId = "acc1", BrokerageAccountId = "bra-1",
            Actions = new() { new TradeAction { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Buy, QuantityType = QuantityType.Shares, Quantity = 5 } }
        };

        var trades = await service.ExecuteActionsAsync(strategy, "AAPL", MakeSnapshot());

        Assert.Single(trades);
        Assert.Equal(5m, trades[0].Quantity);
        cosmo.Verify(c => c.CreateTradeAsync(It.IsAny<Trade>()), Times.Once);
    }

    // ── No brokerage account ──

    [Fact]
    public async Task ExecuteActionsAsync_NoBrokerageAccount_SkipsNonAlertActions()
    {
        var (service, cosmo, _) = CreateSut();

        var strategy = new TradingStrategy
        {
            id = "str-1", AccountId = "acc1", BrokerageAccountId = null,
            Actions = new() { new TradeAction { ActionType = TradeActionType.MarketOrder, Side = TradeSide.Buy, Quantity = 5 } }
        };

        var trades = await service.ExecuteActionsAsync(strategy, "AAPL", MakeSnapshot());

        Assert.Empty(trades);
    }
}
