using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;
using MacroSutra.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace MacroSutra.Tests.Services;

public class PushNotificationServiceTests
{
    private static (
        PushNotificationService service,
        Mock<MacroSutraCosmo> cosmo,
        Mock<IHttpClientFactory> httpClientFactory,
        Mock<IConfiguration> configuration,
        Mock<ILogger<PushNotificationService>> logger
    ) CreateSut(string? fcmServerKey = null)
    {
        var cosmo = new Mock<MacroSutraCosmo>(Mock.Of<IConfiguration>(), "Cosmo:ConnectionString");
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var configuration = new Mock<IConfiguration>();
        var logger = new Mock<ILogger<PushNotificationService>>();

        configuration.Setup(c => c["Firebase:ServerKey"]).Returns(fcmServerKey);

        var service = new PushNotificationService(
            cosmo.Object, httpClientFactory.Object, configuration.Object, logger.Object);

        return (service, cosmo, httpClientFactory, configuration, logger);
    }

    private static TradingStrategy MakeStrategy() => new()
    {
        id = "str-1", AccountId = "acc-pub", Name = "Test Strategy",
        Symbols = new List<string> { "AAPL" }
    };

    private static Trade MakeTrade() => new()
    {
        id = "trade-1", AccountId = "acc-pub", Symbol = "AAPL",
        Side = TradeSide.Buy, Quantity = 10, FilledPrice = 150m,
        Status = TradeStatus.Filled
    };

    private static Mock<HttpMessageHandler> SetupHttpHandler(HttpStatusCode statusCode)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
        return handler;
    }

    private static void SetupHttpClientFactory(Mock<IHttpClientFactory> factory, Mock<HttpMessageHandler> handler)
    {
        // Return a new HttpClient each time, since SendFcmAsync disposes it after each call
        factory.Setup(f => f.CreateClient("FCM")).Returns(() => new HttpClient(handler.Object));
    }

    [Fact]
    public async Task NoFirebaseConfig_ReturnsFalse()
    {
        var (service, cosmo, _, _, _) = CreateSut(fcmServerKey: null);

        var result = await service.SendTradeAlertAsync("acc-sub", MakeTrade(), MakeStrategy());

        Assert.False(result);
        // Should not even query tokens
        cosmo.Verify(c => c.GetPushTokensAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task NoTokens_ReturnsFalse()
    {
        var (service, cosmo, _, _, _) = CreateSut(fcmServerKey: "test-key");

        cosmo.Setup(c => c.GetPushTokensAsync("acc-sub")).ReturnsAsync(new List<PushToken>());

        var result = await service.SendTradeAlertAsync("acc-sub", MakeTrade(), MakeStrategy());

        Assert.False(result);
    }

    [Fact]
    public async Task SendSuccess_ReturnsTrue()
    {
        var (service, cosmo, httpClientFactory, _, _) = CreateSut(fcmServerKey: "test-key");

        var token = new PushToken { id = "tok-1", AccountId = "acc-sub", Token = "device-token-1" };
        cosmo.Setup(c => c.GetPushTokensAsync("acc-sub")).ReturnsAsync(new List<PushToken> { token });
        cosmo.Setup(c => c.UpdatePushTokenAsync(It.IsAny<PushToken>())).ReturnsAsync((PushToken t) => t);

        var handler = SetupHttpHandler(HttpStatusCode.OK);
        SetupHttpClientFactory(httpClientFactory, handler);

        var result = await service.SendTradeAlertAsync("acc-sub", MakeTrade(), MakeStrategy());

        Assert.True(result);
    }

    [Fact]
    public async Task SendFailure_LogsWarning()
    {
        var (service, cosmo, httpClientFactory, _, logger) = CreateSut(fcmServerKey: "test-key");

        var token = new PushToken { id = "tok-1", AccountId = "acc-sub", Token = "device-token-1" };
        cosmo.Setup(c => c.GetPushTokensAsync("acc-sub")).ReturnsAsync(new List<PushToken> { token });

        // Return a failure status code to cause EnsureSuccessStatusCode to throw
        var handler = SetupHttpHandler(HttpStatusCode.InternalServerError);
        SetupHttpClientFactory(httpClientFactory, handler);

        var result = await service.SendTradeAlertAsync("acc-sub", MakeTrade(), MakeStrategy());

        // Should return false because the send failed (exception caught)
        Assert.False(result);
        // Verify warning was logged
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RegisterToken_DelegatesToCosmo()
    {
        var (service, cosmo, _, _, _) = CreateSut(fcmServerKey: "test-key");

        var pushToken = new PushToken
        {
            id = "tok-1", AccountId = "acc-sub", UserId = "user-1",
            Token = "device-token-1", Platform = "android"
        };

        cosmo.Setup(c => c.CreatePushTokenAsync(pushToken)).ReturnsAsync(pushToken);

        var result = await service.RegisterTokenAsync(pushToken);

        Assert.Equal("tok-1", result.id);
        cosmo.Verify(c => c.CreatePushTokenAsync(pushToken), Times.Once);
    }

    [Fact]
    public async Task RemoveToken_DelegatesToCosmo()
    {
        var (service, cosmo, _, _, _) = CreateSut(fcmServerKey: "test-key");

        cosmo.Setup(c => c.DeletePushTokenAsync("tok-1", "acc-sub")).Returns(Task.CompletedTask);

        await service.RemoveTokenAsync("tok-1", "acc-sub");

        cosmo.Verify(c => c.DeletePushTokenAsync("tok-1", "acc-sub"), Times.Once);
    }

    [Fact]
    public async Task LastUsedUtc_UpdatedOnSuccess()
    {
        var (service, cosmo, httpClientFactory, _, _) = CreateSut(fcmServerKey: "test-key");

        var beforeSend = DateTime.UtcNow;
        var token = new PushToken { id = "tok-1", AccountId = "acc-sub", Token = "device-token-1", LastUsedUtc = null };
        cosmo.Setup(c => c.GetPushTokensAsync("acc-sub")).ReturnsAsync(new List<PushToken> { token });
        cosmo.Setup(c => c.UpdatePushTokenAsync(It.IsAny<PushToken>())).ReturnsAsync((PushToken t) => t);

        var handler = SetupHttpHandler(HttpStatusCode.OK);
        SetupHttpClientFactory(httpClientFactory, handler);

        await service.SendTradeAlertAsync("acc-sub", MakeTrade(), MakeStrategy());

        // Verify that UpdatePushTokenAsync was called with a token that has LastUsedUtc set
        cosmo.Verify(c => c.UpdatePushTokenAsync(
            It.Is<PushToken>(t => t.LastUsedUtc != null && t.LastUsedUtc >= beforeSend)),
            Times.Once);
    }

    [Fact]
    public async Task MultipleTokens_SendsToAll()
    {
        var (service, cosmo, httpClientFactory, _, _) = CreateSut(fcmServerKey: "test-key");

        var tokens = new List<PushToken>
        {
            new() { id = "tok-1", AccountId = "acc-sub", Token = "device-token-1" },
            new() { id = "tok-2", AccountId = "acc-sub", Token = "device-token-2" },
            new() { id = "tok-3", AccountId = "acc-sub", Token = "device-token-3" }
        };

        cosmo.Setup(c => c.GetPushTokensAsync("acc-sub")).ReturnsAsync(tokens);
        cosmo.Setup(c => c.UpdatePushTokenAsync(It.IsAny<PushToken>())).ReturnsAsync((PushToken t) => t);

        var handler = SetupHttpHandler(HttpStatusCode.OK);
        SetupHttpClientFactory(httpClientFactory, handler);

        var result = await service.SendTradeAlertAsync("acc-sub", MakeTrade(), MakeStrategy());

        Assert.True(result);
        // Verify all three tokens were updated (one per successful send)
        cosmo.Verify(c => c.UpdatePushTokenAsync(It.IsAny<PushToken>()), Times.Exactly(3));
    }
}
