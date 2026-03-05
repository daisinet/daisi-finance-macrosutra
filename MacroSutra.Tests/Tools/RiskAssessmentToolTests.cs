using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Tools;

namespace MacroSutra.Tests.Tools;

public class RiskAssessmentToolTests
{
    // ── Metadata ──

    [Fact]
    public void Id_IsCorrect() => Assert.Equal("macrosutra-risk-assessment", new RiskAssessmentTool().Id);

    [Fact]
    public void Parameters_HasTwoEntries() => Assert.Equal(2, new RiskAssessmentTool().Parameters.Length);

    // ── BuildPrompt ──

    [Fact]
    public void BuildPrompt_ContainsStrategyContext()
    {
        var prompt = RiskAssessmentTool.BuildPrompt("Strategy: RSI Dip Buy\nSymbols: AAPL");
        Assert.Contains("RSI Dip Buy", prompt);
        Assert.Contains("AAPL", prompt);
        Assert.Contains("Risk Rating", prompt);
        Assert.Contains("Recommendations", prompt);
    }

    [Fact]
    public void BuildPrompt_ContainsAllSections()
    {
        var prompt = RiskAssessmentTool.BuildPrompt("test context");
        Assert.Contains("Risk Rating", prompt);
        Assert.Contains("Key Risks", prompt);
        Assert.Contains("Strengths", prompt);
        Assert.Contains("Recommendations", prompt);
    }
}
