using MacroSutra.UI.Services;

namespace MacroSutra.Tests.UI;

public class DragPayloadTests
{
    [Fact]
    public void ParseDragPayload_Condition_ReturnsKindAndType()
    {
        var (kind, value) = ConditionWizardHelper.ParseDragPayload("condition:Price");
        Assert.Equal("condition", kind);
        Assert.Equal("Price", value);
    }

    [Fact]
    public void ParseDragPayload_Action_ReturnsKindAndType()
    {
        var (kind, value) = ConditionWizardHelper.ParseDragPayload("action:MarketOrder");
        Assert.Equal("action", kind);
        Assert.Equal("MarketOrder", value);
    }

    [Fact]
    public void ParseDragPayload_Group_ReturnsKindOnly()
    {
        var (kind, value) = ConditionWizardHelper.ParseDragPayload("group");
        Assert.Equal("group", kind);
        Assert.Equal("", value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseDragPayload_NullOrEmpty_ReturnsEmpty(string? payload)
    {
        var (kind, value) = ConditionWizardHelper.ParseDragPayload(payload);
        Assert.Equal("", kind);
        Assert.Equal("", value);
    }
}
