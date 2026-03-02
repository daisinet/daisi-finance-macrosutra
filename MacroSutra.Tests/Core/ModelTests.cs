using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Tests.Core;

public class ModelTests
{
    [Fact]
    public void MacroSutraUser_DefaultValues_AreCorrect()
    {
        var user = new MacroSutraUser();
        Assert.Equal("", user.id);
        Assert.Equal(nameof(MacroSutraUser), user.Type);
        Assert.Equal("", user.AccountId);
        Assert.Equal(MacroSutraRole.Viewer, user.Role);
        Assert.True(user.IsActive);
        Assert.Null(user.UpdatedUtc);
    }

    [Fact]
    public void TradingStrategy_DefaultValues_AreCorrect()
    {
        var strategy = new TradingStrategy();
        Assert.Equal(nameof(TradingStrategy), strategy.Type);
        Assert.Empty(strategy.Symbols);
        Assert.Empty(strategy.Conditions);
        Assert.Empty(strategy.Actions);
        Assert.Equal(LogicGroupType.And, strategy.LogicGroup);
        Assert.Equal(SizingMode.Fixed, strategy.SizingMode);
        Assert.False(strategy.IsActive);
    }

    [Fact]
    public void Trade_DefaultValues_AreCorrect()
    {
        var trade = new Trade();
        Assert.Equal(nameof(Trade), trade.Type);
        Assert.Equal(TradeSide.Buy, trade.Side);
        Assert.Equal(TradeStatus.Pending, trade.Status);
        Assert.Null(trade.FilledPrice);
        Assert.Null(trade.FilledUtc);
    }

    [Fact]
    public void Position_ComputedProperties_CalculateCorrectly()
    {
        var position = new Position
        {
            Quantity = 100,
            AverageCost = 50m,
            CurrentPrice = 55m
        };

        Assert.Equal(5500m, position.MarketValue);
        Assert.Equal(500m, position.UnrealizedPnL);
    }

    [Fact]
    public void Position_ComputedProperties_NullWhenNoPriceData()
    {
        var position = new Position
        {
            Quantity = 100,
            AverageCost = 50m,
            CurrentPrice = null
        };

        Assert.Null(position.MarketValue);
        Assert.Null(position.UnrealizedPnL);
    }

    [Fact]
    public void BrokerageAccount_DefaultValues_AreCorrect()
    {
        var account = new BrokerageAccount();
        Assert.Equal(nameof(BrokerageAccount), account.Type);
        Assert.Equal(BrokerageProvider.Paper, account.Provider);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void Subscription_DefaultValues_AreCorrect()
    {
        var sub = new Subscription();
        Assert.Equal(nameof(Subscription), sub.Type);
        Assert.Equal(SubscriptionActionType.Mirror, sub.ActionType);
        Assert.Equal(1.0m, sub.ScaleFactor);
        Assert.True(sub.IsActive);
    }

    [Fact]
    public void TriggerCondition_GeneratesUniqueIds()
    {
        var c1 = new TriggerCondition();
        var c2 = new TriggerCondition();
        Assert.NotEqual(c1.ConditionId, c2.ConditionId);
    }

    [Fact]
    public void TradeAction_GeneratesUniqueIds()
    {
        var a1 = new TradeAction();
        var a2 = new TradeAction();
        Assert.NotEqual(a1.ActionId, a2.ActionId);
    }
}
