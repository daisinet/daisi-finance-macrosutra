using MacroSutra.Services;

namespace MacroSutra.Tests.Services;

public class StrategyTemplateServiceTests
{
    private static StrategyTemplateService CreateSut() => new();

    [Fact]
    public void GetTemplates_ReturnsAll()
    {
        var service = CreateSut();

        var templates = service.GetTemplates();

        Assert.Equal(5, templates.Count);
    }

    [Fact]
    public void GetTemplates_AllHaveRequiredFields()
    {
        var service = CreateSut();

        var templates = service.GetTemplates();

        foreach (var template in templates)
        {
            Assert.False(string.IsNullOrWhiteSpace(template.Id), $"Template Id should not be empty");
            Assert.False(string.IsNullOrWhiteSpace(template.Name), $"Template '{template.Id}' Name should not be empty");
            Assert.False(string.IsNullOrWhiteSpace(template.Description), $"Template '{template.Id}' Description should not be empty");
            Assert.False(string.IsNullOrWhiteSpace(template.Category), $"Template '{template.Id}' Category should not be empty");
        }
    }

    [Fact]
    public void GetTemplate_ValidId_ReturnsTemplate()
    {
        var service = CreateSut();

        var template = service.GetTemplate("rsi-oversold");

        Assert.NotNull(template);
        Assert.Equal("rsi-oversold", template!.Id);
        Assert.Equal("RSI Oversold Bounce", template.Name);
    }

    [Fact]
    public void GetTemplate_CaseInsensitive()
    {
        var service = CreateSut();

        var upper = service.GetTemplate("RSI-OVERSOLD");
        var mixed = service.GetTemplate("Rsi-Oversold");
        var lower = service.GetTemplate("rsi-oversold");

        Assert.NotNull(upper);
        Assert.NotNull(mixed);
        Assert.NotNull(lower);
        Assert.Equal(upper!.Id, lower!.Id);
        Assert.Equal(mixed!.Id, lower.Id);
    }

    [Fact]
    public void GetTemplate_InvalidId_ReturnsNull()
    {
        var service = CreateSut();

        var template = service.GetTemplate("nonexistent-strategy");

        Assert.Null(template);
    }
}
