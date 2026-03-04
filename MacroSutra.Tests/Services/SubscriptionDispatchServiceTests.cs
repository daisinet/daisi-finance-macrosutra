using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace MacroSutra.Tests.Services;

public class SubscriptionDispatchServiceTests
{
    private static (
        SubscriptionDispatchService service,
        Mock<MacroSutraCosmo> cosmo,
        Mock<BrokerageProviderFactory> providerFactory,
        Mock<EmailNotificationService> emailService,
        Mock<WebhookDispatchService> webhookService
    ) CreateSut()
    {
        var config = Mock.Of<IConfiguration>();
        var cosmo = new Mock<MacroSutraCosmo>(config, "Cosmo:ConnectionString");
        var providerFactory = new Mock<BrokerageProviderFactory>(Mock.Of<IServiceProvider>());
        var portfolioService = new PortfolioService(cosmo.Object, providerFactory.Object);
        var tradeService = new TradeService(cosmo.Object);
        var emailService = new Mock<EmailNotificationService>(
            Mock.Of<IHttpClientFactory>(), config, Mock.Of<ILogger<EmailNotificationService>>());
        var webhookService = new Mock<WebhookDispatchService>(
            Mock.Of<IHttpClientFactory>(), Mock.Of<ILogger<WebhookDispatchService>>());

        var pushService = new Mock<PushNotificationService>(
            cosmo.Object, Mock.Of<IHttpClientFactory>(), config, Mock.Of<ILogger<PushNotificationService>>());

        var service = new SubscriptionDispatchService(
            cosmo.Object, providerFactory.Object, portfolioService, tradeService,
            emailService.Object, webhookService.Object, pushService.Object,
            Mock.Of<ILogger<SubscriptionDispatchService>>());

        return (service, cosmo, providerFactory, emailService, webhookService);
    }

    private static TradingStrategy CreateStrategy() => new()
    {
        id = "str-1", AccountId = "acc-pub", Name = "Test Strategy",
        Symbols = new() { "AAPL" }
    };

    private static Trade CreateTrade() => new()
    {
        id = "trade-1", AccountId = "acc-pub", Symbol = "AAPL",
        Side = TradeSide.Buy, Quantity = 10, FilledPrice = 150m,
        Status = TradeStatus.Filled
    };

    [Fact]
    public async Task DispatchAsync_NoSubscribers_NoOp()
    {
        var (service, cosmo, _, _, _) = CreateSut();
        var strategy = CreateStrategy();

        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1"))
             .ReturnsAsync(new List<Subscription>());

        await service.DispatchAsync(strategy, new List<Trade> { CreateTrade() });

        // No subscription actions should be created
        cosmo.Verify(c => c.CreateSubscriptionActionAsync(It.IsAny<SubscriptionAction>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_EmptyTrades_NoOp()
    {
        var (service, cosmo, _, _, _) = CreateSut();
        var strategy = CreateStrategy();

        await service.DispatchAsync(strategy, new List<Trade>());

        cosmo.Verify(c => c.GetSubscriptionsByStrategyAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_EmailAction_CallsEmailService()
    {
        var (service, cosmo, _, emailService, _) = CreateSut();
        var strategy = CreateStrategy();
        var trade = CreateTrade();

        var subscription = new Subscription
        {
            id = "sub-1", AccountId = "acc-sub", StrategyId = "str-1",
            ActionType = SubscriptionActionType.Email,
            NotificationEmail = "test@example.com",
            IsActive = true
        };

        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1"))
             .ReturnsAsync(new List<Subscription> { subscription });
        emailService.Setup(e => e.SendTradeAlertAsync("test@example.com", "", trade, strategy))
                    .ReturnsAsync(true);
        cosmo.Setup(c => c.CreateSubscriptionActionAsync(It.IsAny<SubscriptionAction>()))
             .ReturnsAsync((SubscriptionAction a) => a);

        await service.DispatchAsync(strategy, new List<Trade> { trade });

        emailService.Verify(e => e.SendTradeAlertAsync("test@example.com", "", trade, strategy), Times.Once);
        cosmo.Verify(c => c.CreateSubscriptionActionAsync(
            It.Is<SubscriptionAction>(a => a.Success && a.ActionType == SubscriptionActionType.Email)),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WebhookAction_CallsWebhookService()
    {
        var (service, cosmo, _, _, webhookService) = CreateSut();
        var strategy = CreateStrategy();
        var trade = CreateTrade();

        var subscription = new Subscription
        {
            id = "sub-1", AccountId = "acc-sub", StrategyId = "str-1",
            ActionType = SubscriptionActionType.Webhook,
            WebhookUrl = "https://hooks.example.com/trade",
            IsActive = true
        };

        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1"))
             .ReturnsAsync(new List<Subscription> { subscription });
        webhookService.Setup(w => w.DispatchAsync("https://hooks.example.com/trade", trade, strategy))
                      .ReturnsAsync((true, (int?)200));
        cosmo.Setup(c => c.CreateSubscriptionActionAsync(It.IsAny<SubscriptionAction>()))
             .ReturnsAsync((SubscriptionAction a) => a);

        await service.DispatchAsync(strategy, new List<Trade> { trade });

        webhookService.Verify(w => w.DispatchAsync("https://hooks.example.com/trade", trade, strategy), Times.Once);
        cosmo.Verify(c => c.CreateSubscriptionActionAsync(
            It.Is<SubscriptionAction>(a => a.Success && a.WebhookStatusCode == 200)),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_MirrorAction_NoBrokerageAccount_RecordsFailure()
    {
        var (service, cosmo, _, _, _) = CreateSut();
        var strategy = CreateStrategy();
        var trade = CreateTrade();

        var subscription = new Subscription
        {
            id = "sub-1", AccountId = "acc-sub", StrategyId = "str-1",
            ActionType = SubscriptionActionType.Mirror,
            BrokerageAccountId = null, // no account
            IsActive = true
        };

        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1"))
             .ReturnsAsync(new List<Subscription> { subscription });
        cosmo.Setup(c => c.CreateSubscriptionActionAsync(It.IsAny<SubscriptionAction>()))
             .ReturnsAsync((SubscriptionAction a) => a);

        await service.DispatchAsync(strategy, new List<Trade> { trade });

        cosmo.Verify(c => c.CreateSubscriptionActionAsync(
            It.Is<SubscriptionAction>(a => !a.Success && a.ErrorMessage!.Contains("brokerage"))),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ScaledMirror_ScalesQuantity()
    {
        var (service, cosmo, providerFactory, _, _) = CreateSut();
        var strategy = CreateStrategy();
        var trade = CreateTrade(); // Quantity = 10

        var subscription = new Subscription
        {
            id = "sub-1", AccountId = "acc-sub", StrategyId = "str-1",
            ActionType = SubscriptionActionType.ScaledMirror,
            ScaleFactor = 0.5m,
            BrokerageAccountId = "brok-1",
            IsActive = true
        };

        var brokerageAccount = new BrokerageAccount
        {
            id = "brok-1", AccountId = "acc-sub", Provider = BrokerageProvider.Paper,
            IsActive = true, CredentialData = "{}"
        };

        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1"))
             .ReturnsAsync(new List<Subscription> { subscription });
        cosmo.Setup(c => c.GetBrokerageAccountAsync("brok-1", "acc-sub"))
             .ReturnsAsync(brokerageAccount);
        cosmo.Setup(c => c.CreateTradeAsync(It.IsAny<Trade>()))
             .ReturnsAsync((Trade t) => { t.id = "mirrored-trade"; return t; });
        cosmo.Setup(c => c.CreateSubscriptionActionAsync(It.IsAny<SubscriptionAction>()))
             .ReturnsAsync((SubscriptionAction a) => a);

        var mockProvider = new Mock<IBrokerageProvider>();
        mockProvider.Setup(p => p.PlaceOrderAsync(It.IsAny<string>(), It.IsAny<Trade>()))
                    .ReturnsAsync("ext-order-1");
        providerFactory.Setup(f => f.GetProvider(BrokerageProvider.Paper))
                       .Returns(mockProvider.Object);

        cosmo.Setup(c => c.GetTradeAsync(It.IsAny<string>(), It.IsAny<string>()))
             .ReturnsAsync((string id, string acct) => new Trade { id = id, AccountId = acct, Status = TradeStatus.Pending });
        cosmo.Setup(c => c.UpdateTradeAsync(It.IsAny<Trade>()))
             .ReturnsAsync((Trade t) => t);

        await service.DispatchAsync(strategy, new List<Trade> { trade });

        // Verify trade was recorded with scaled quantity (10 * 0.5 = 5)
        cosmo.Verify(c => c.CreateTradeAsync(It.Is<Trade>(t => t.Quantity == 5.0m)), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_FailedDispatch_DoesNotBlockOthers()
    {
        var (service, cosmo, _, emailService, _) = CreateSut();
        var strategy = CreateStrategy();
        var trade = CreateTrade();

        var sub1 = new Subscription
        {
            id = "sub-1", AccountId = "acc-sub1", StrategyId = "str-1",
            ActionType = SubscriptionActionType.Email,
            NotificationEmail = "bad@example.com", IsActive = true
        };
        var sub2 = new Subscription
        {
            id = "sub-2", AccountId = "acc-sub2", StrategyId = "str-1",
            ActionType = SubscriptionActionType.Email,
            NotificationEmail = "good@example.com", IsActive = true
        };

        cosmo.Setup(c => c.GetSubscriptionsByStrategyAsync("str-1"))
             .ReturnsAsync(new List<Subscription> { sub1, sub2 });

        // First subscriber throws
        emailService.Setup(e => e.SendTradeAlertAsync("bad@example.com", "", trade, strategy))
                    .ThrowsAsync(new Exception("SMTP error"));
        // Second subscriber succeeds
        emailService.Setup(e => e.SendTradeAlertAsync("good@example.com", "", trade, strategy))
                    .ReturnsAsync(true);
        cosmo.Setup(c => c.CreateSubscriptionActionAsync(It.IsAny<SubscriptionAction>()))
             .ReturnsAsync((SubscriptionAction a) => a);

        await service.DispatchAsync(strategy, new List<Trade> { trade });

        // Both should have been attempted
        emailService.Verify(e => e.SendTradeAlertAsync("bad@example.com", "", trade, strategy), Times.Once);
        emailService.Verify(e => e.SendTradeAlertAsync("good@example.com", "", trade, strategy), Times.Once);
    }
}
