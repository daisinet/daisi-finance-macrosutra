using MacroSutra.Core.Enums;

namespace MacroSutra.Tests.Core;

public class EnumTests
{
    [Fact]
    public void MacroSutraRole_Viewer_IsZero()
    {
        Assert.Equal(0, (int)MacroSutraRole.Viewer);
    }

    [Fact]
    public void MacroSutraRole_Owner_IsHighest()
    {
        Assert.True(MacroSutraRole.Owner > MacroSutraRole.Manager);
        Assert.True(MacroSutraRole.Manager > MacroSutraRole.Trader);
        Assert.True(MacroSutraRole.Trader > MacroSutraRole.Viewer);
    }

    [Fact]
    public void MacroSutraRole_CanCompare_ForPermissions()
    {
        var userRole = MacroSutraRole.Manager;
        Assert.True(userRole >= MacroSutraRole.Trader);
        Assert.False(userRole >= MacroSutraRole.Owner);
    }

    [Fact]
    public void TradeStatus_Pending_IsDefault()
    {
        Assert.Equal(0, (int)TradeStatus.Pending);
    }

    [Fact]
    public void BrokerageProvider_Paper_IsDefault()
    {
        Assert.Equal(0, (int)BrokerageProvider.Paper);
    }

    [Fact]
    public void ConditionOperator_HasExpectedValues()
    {
        Assert.Equal(0, (int)ConditionOperator.GreaterThan);
        Assert.Equal(5, (int)ConditionOperator.CrossesAbove);
        Assert.Equal(6, (int)ConditionOperator.CrossesBelow);
    }
}
