using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.UI.Services;

namespace MacroSutra.Tests.UI;

public class ConditionWizardHelperTests
{
    [Fact]
    public void GetAllConditionTypes_ReturnsAllEnumValues()
    {
        var types = ConditionWizardHelper.GetAllConditionTypes();
        foreach (var ct in Enum.GetValues<ConditionType>())
            Assert.Contains(types, t => t.Type == ct);
    }

    [Fact]
    public void GetConditionTypeInfo_ReturnsCorrectInfo()
    {
        var info = ConditionWizardHelper.GetConditionTypeInfo(ConditionType.RSI);
        Assert.Equal("RSI", info.Name);
        Assert.Equal("Technical Indicators", info.Category);
    }

    [Fact]
    public void GetConditionTypeInfo_UnknownType_ReturnsFallback()
    {
        var info = ConditionWizardHelper.GetConditionTypeInfo((ConditionType)999);
        Assert.Equal("999", info.Name);
        Assert.Equal("Other", info.Category);
    }

    [Fact]
    public void GetOperatorsForType_TimeOfDay_ExcludesCrossover()
    {
        var ops = ConditionWizardHelper.GetOperatorsForType(ConditionType.TimeOfDay);
        Assert.DoesNotContain(ConditionOperator.CrossesAbove, ops);
        Assert.DoesNotContain(ConditionOperator.CrossesBelow, ops);
    }

    [Fact]
    public void GetOperatorsForType_DayOfWeek_OnlyEqual()
    {
        var ops = ConditionWizardHelper.GetOperatorsForType(ConditionType.DayOfWeek);
        Assert.Single(ops);
        Assert.Equal(ConditionOperator.Equal, ops[0]);
    }

    [Fact]
    public void GetOperatorsForType_Price_ReturnsAll()
    {
        var ops = ConditionWizardHelper.GetOperatorsForType(ConditionType.Price);
        Assert.Equal(Enum.GetValues<ConditionOperator>().Length, ops.Length);
    }

    [Theory]
    [InlineData(ConditionType.TimeOfDay, ConditionWizardHelper.InputMode.TimePicker)]
    [InlineData(ConditionType.DayOfWeek, ConditionWizardHelper.InputMode.DaySelector)]
    [InlineData(ConditionType.Price, ConditionWizardHelper.InputMode.Numeric)]
    [InlineData(ConditionType.RSI, ConditionWizardHelper.InputMode.Numeric)]
    public void GetInputMode_ReturnsCorrectMode(ConditionType type, ConditionWizardHelper.InputMode expected)
    {
        Assert.Equal(expected, ConditionWizardHelper.GetInputMode(type));
    }

    [Theory]
    [InlineData(ConditionType.MovingAverage, true)]
    [InlineData(ConditionType.RSI, true)]
    [InlineData(ConditionType.MACD, true)]
    [InlineData(ConditionType.Price, false)]
    [InlineData(ConditionType.TimeOfDay, false)]
    public void RequiresPeriod_ReturnsCorrectly(ConditionType type, bool expected)
    {
        Assert.Equal(expected, ConditionWizardHelper.RequiresPeriod(type));
    }

    [Fact]
    public void DescribeCondition_RSI_IncludesPeriod()
    {
        var condition = new TriggerCondition
        {
            ConditionType = ConditionType.RSI,
            Operator = ConditionOperator.LessThan,
            Value = 30,
            Period = 14
        };
        var desc = ConditionWizardHelper.DescribeCondition(condition);
        Assert.Contains("RSI", desc);
        Assert.Contains("< 30", desc);
        Assert.Contains("14p", desc);
    }

    [Fact]
    public void DescribeCondition_Price_NoPeroid()
    {
        var condition = new TriggerCondition
        {
            ConditionType = ConditionType.Price,
            Operator = ConditionOperator.GreaterThan,
            Value = 150.50m
        };
        var desc = ConditionWizardHelper.DescribeCondition(condition);
        Assert.Contains("Price", desc);
        Assert.Contains("> 150.50", desc);
        Assert.DoesNotContain("p)", desc);
    }

    [Fact]
    public void DescribeCondition_TimeOfDay_FormatsAsTime()
    {
        var condition = new TriggerCondition
        {
            ConditionType = ConditionType.TimeOfDay,
            Operator = ConditionOperator.GreaterThan,
            Value = 570 // 9:30 AM
        };
        var desc = ConditionWizardHelper.DescribeCondition(condition);
        Assert.Contains("after", desc);
        Assert.Contains("9:30 AM", desc);
    }

    [Fact]
    public void DescribeCondition_DayOfWeek_FormatsAsDayName()
    {
        var condition = new TriggerCondition
        {
            ConditionType = ConditionType.DayOfWeek,
            Operator = ConditionOperator.Equal,
            Value = 1 // Monday
        };
        var desc = ConditionWizardHelper.DescribeCondition(condition);
        Assert.Contains("Monday", desc);
    }

    [Fact]
    public void GetConditionColor_MarketData_ReturnsCorrect()
    {
        Assert.Equal("market-data", ConditionWizardHelper.GetConditionColor(ConditionType.Price));
        Assert.Equal("market-data", ConditionWizardHelper.GetConditionColor(ConditionType.Volume));
        Assert.Equal("technical", ConditionWizardHelper.GetConditionColor(ConditionType.RSI));
        Assert.Equal("time-based", ConditionWizardHelper.GetConditionColor(ConditionType.TimeOfDay));
        Assert.Equal("custom", ConditionWizardHelper.GetConditionColor(ConditionType.Custom));
    }
}
