using System.Globalization;
using System.Text;
using MacroSutra.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MacroSutra.Services;

/// <summary>
/// Exports trades to CSV and PDF formats.
/// </summary>
public class TradeExportService(TradeService tradeService)
{
    static TradeExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Exports trades as CSV byte array.
    /// </summary>
    public async Task<byte[]> ExportCsvAsync(string accountId, string? symbol = null, string? strategyId = null)
    {
        var trades = await tradeService.GetTradesAsync(accountId, symbol, null, strategyId);
        return GenerateCsv(trades);
    }

    internal static byte[] GenerateCsv(List<Trade> trades)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Symbol,Side,Type,Qty,FilledQty,FilledPrice,Status,StrategyId,Notes");

        foreach (var t in trades)
        {
            sb.Append(CsvEscape(t.CreatedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))); sb.Append(',');
            sb.Append(CsvEscape(t.Symbol)); sb.Append(',');
            sb.Append(CsvEscape(t.Side.ToString())); sb.Append(',');
            sb.Append(CsvEscape(t.OrderType.ToString())); sb.Append(',');
            sb.Append(t.Quantity.ToString(CultureInfo.InvariantCulture)); sb.Append(',');
            sb.Append(t.FilledQuantity?.ToString(CultureInfo.InvariantCulture) ?? ""); sb.Append(',');
            sb.Append(t.FilledPrice?.ToString(CultureInfo.InvariantCulture) ?? ""); sb.Append(',');
            sb.Append(CsvEscape(t.Status.ToString())); sb.Append(',');
            sb.Append(CsvEscape(t.StrategyId ?? "")); sb.Append(',');
            sb.Append(CsvEscape(t.Notes ?? ""));
            sb.AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    internal static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    /// <summary>
    /// Exports trades as PDF byte array.
    /// </summary>
    public async Task<byte[]> ExportPdfAsync(string accountId, string? symbol = null, string? strategyId = null)
    {
        var trades = await tradeService.GetTradesAsync(accountId, symbol, null, strategyId);
        return GeneratePdf(trades, symbol);
    }

    internal static byte[] GeneratePdf(List<Trade> trades, string? symbolFilter = null)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);

                page.Header().Column(col =>
                {
                    col.Item().Text("MacroSutra — Trade Report").FontSize(18).Bold();
                    col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(10).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrEmpty(symbolFilter))
                        col.Item().Text($"Symbol: {symbolFilter}").FontSize(10);
                    col.Item().PaddingVertical(5).LineHorizontal(1);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Date
                        columns.RelativeColumn(1); // Symbol
                        columns.RelativeColumn(1); // Side
                        columns.RelativeColumn(1); // Type
                        columns.RelativeColumn(1); // Qty
                        columns.RelativeColumn(1); // Filled Price
                        columns.RelativeColumn(1); // Status
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Date").Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Symbol").Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Side").Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Type").Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Qty").Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Filled Price").Bold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Status").Bold().FontSize(9);
                    });

                    foreach (var t in trades)
                    {
                        table.Cell().Padding(3).Text(t.CreatedUtc.ToString("MM/dd/yy HH:mm")).FontSize(8);
                        table.Cell().Padding(3).Text(t.Symbol).FontSize(8);
                        table.Cell().Padding(3).Text(t.Side.ToString()).FontSize(8);
                        table.Cell().Padding(3).Text(t.OrderType.ToString()).FontSize(8);
                        table.Cell().Padding(3).Text(t.Quantity.ToString("N2")).FontSize(8);
                        table.Cell().Padding(3).Text(t.FilledPrice?.ToString("C") ?? "—").FontSize(8);
                        table.Cell().Padding(3).Text(t.Status.ToString()).FontSize(8);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        });

        return document.GeneratePdf();
    }
}
