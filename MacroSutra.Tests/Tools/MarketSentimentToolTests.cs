using MacroSutra.Tools;

namespace MacroSutra.Tests.Tools;

public class MarketSentimentToolTests
{
    // ── Metadata ──

    [Fact]
    public void Id_IsCorrect() => Assert.Equal("macrosutra-market-sentiment", new MarketSentimentTool().Id);

    [Fact]
    public void Parameters_HasOneEntry() => Assert.Single(new MarketSentimentTool().Parameters);

    // ── BuildPrompt ──

    [Fact]
    public void BuildPrompt_ContainsMarketContext()
    {
        var prompt = MarketSentimentTool.BuildPrompt("Symbol: AAPL\nPrice: $185.50");
        Assert.Contains("AAPL", prompt);
        Assert.Contains("185.50", prompt);
        Assert.Contains("Sentiment", prompt);
    }

    [Fact]
    public void BuildPrompt_ContainsAllSections()
    {
        var prompt = MarketSentimentTool.BuildPrompt("test data");
        Assert.Contains("Sentiment", prompt);
        Assert.Contains("Key Observations", prompt);
        Assert.Contains("Technical Outlook", prompt);
        Assert.Contains("Trading Signals", prompt);
    }

    [Fact]
    public void BuildPrompt_ContainsDisclaimer()
    {
        var prompt = MarketSentimentTool.BuildPrompt("test");
        Assert.Contains("not financial advice", prompt);
    }
}
