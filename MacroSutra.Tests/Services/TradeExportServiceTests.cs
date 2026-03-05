using System.Text;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;

namespace MacroSutra.Tests.Services;

/// <summary>
/// WU2+WU3: CSV and PDF export tests.
/// Tests use the internal static methods directly to avoid Cosmos DB dependencies.
/// </summary>
public class TradeExportServiceTests
{
    private static List<Trade> MakeTrades() =>
    [
        new()
        {
            Symbol = "AAPL", Side = TradeSide.Buy, OrderType = TradeActionType.MarketOrder,
            Quantity = 10, Status = TradeStatus.Filled, FilledPrice = 150.50m, FilledQuantity = 10,
            StrategyId = "strat-1", Notes = "Test trade"
        },
        new()
        {
            Symbol = "MSFT", Side = TradeSide.Sell, OrderType = TradeActionType.LimitOrder,
            Quantity = 5, Status = TradeStatus.Submitted, LimitPrice = 400m,
            StrategyId = null, Notes = null
        }
    ];

    // ── CSV Tests ──

    [Fact]
    public void GenerateCsv_EmptyTrades_ReturnsHeaderOnly()
    {
        var csv = TradeExportService.GenerateCsv([]);
        var text = Encoding.UTF8.GetString(csv);
        var lines = text.TrimEnd().Split('\n');
        Assert.Single(lines);
        Assert.StartsWith("Date,Symbol,Side,Type,Qty", lines[0]);
    }

    [Fact]
    public void GenerateCsv_ColumnsPresent()
    {
        var csv = TradeExportService.GenerateCsv(MakeTrades());
        var text = Encoding.UTF8.GetString(csv);
        var header = text.Split('\n')[0];
        Assert.Contains("Date", header);
        Assert.Contains("Symbol", header);
        Assert.Contains("Side", header);
        Assert.Contains("Type", header);
        Assert.Contains("Qty", header);
        Assert.Contains("FilledQty", header);
        Assert.Contains("FilledPrice", header);
        Assert.Contains("Status", header);
        Assert.Contains("StrategyId", header);
        Assert.Contains("Notes", header);
    }

    [Fact]
    public void GenerateCsv_ContainsSymbol()
    {
        var csv = TradeExportService.GenerateCsv(MakeTrades());
        var text = Encoding.UTF8.GetString(csv);
        Assert.Contains("AAPL", text);
        Assert.Contains("MSFT", text);
    }

    [Fact]
    public void CsvEscape_CommaInValue_WrapsInQuotes()
    {
        var escaped = TradeExportService.CsvEscape("hello, world");
        Assert.Equal("\"hello, world\"", escaped);
    }

    [Fact]
    public void CsvEscape_QuoteInValue_DoublesQuotes()
    {
        var escaped = TradeExportService.CsvEscape("say \"hello\"");
        Assert.Equal("\"say \"\"hello\"\"\"", escaped);
    }

    [Fact]
    public void CsvEscape_PlainValue_Unchanged()
    {
        var escaped = TradeExportService.CsvEscape("AAPL");
        Assert.Equal("AAPL", escaped);
    }

    // ── PDF Tests ──

    [Fact]
    public void GeneratePdf_NonEmptyTrades_ReturnsNonEmptyByteArray()
    {
        var pdf = TradeExportService.GeneratePdf(MakeTrades());
        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 100, "PDF should be a non-trivial size");
    }

    [Fact]
    public void GeneratePdf_EmptyTrades_StillGeneratesValidPdf()
    {
        var pdf = TradeExportService.GeneratePdf([]);
        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
    }

    [Fact]
    public void GeneratePdf_StartsWithPdfHeader()
    {
        var pdf = TradeExportService.GeneratePdf(MakeTrades());
        // PDF files start with %PDF
        var header = Encoding.ASCII.GetString(pdf, 0, 4);
        Assert.Equal("%PDF", header);
    }
}
