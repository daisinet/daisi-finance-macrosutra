using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;

namespace MacroSutra.Services;

/// <summary>
/// Analyzes portfolio positions for tax-loss harvesting opportunities.
/// Read-only — does not execute trades. Uses a 20% estimated tax rate and
/// a 30-day wash sale window.
/// </summary>
public class TaxLossHarvestingService(
    PortfolioService portfolioService,
    TradeService tradeService)
{
    private const decimal EstimatedTaxRate = 0.20m;
    private const int WashSaleWindowDays = 30;

    /// <summary>
    /// Scans all positions for unrealized losses, checks wash sale risk
    /// against recent trades, and returns a report.
    /// </summary>
    public async Task<TaxLossHarvestingReport> AnalyzeAsync(string accountId, string? brokerageAccountId = null)
    {
        var positions = await portfolioService.GetPositionsAsync(accountId, brokerageAccountId);
        var recentTrades = await tradeService.GetTradesAsync(accountId);
        var washSaleCutoff = DateTime.UtcNow.AddDays(-WashSaleWindowDays);

        var report = new TaxLossHarvestingReport();

        foreach (var position in positions)
        {
            if (!position.CurrentPrice.HasValue || position.CurrentPrice <= 0)
                continue;
            if (!position.UnrealizedPnL.HasValue || position.UnrealizedPnL >= 0)
                continue;
            if (position.IsOption)
                continue;

            var loss = Math.Abs(position.UnrealizedPnL.Value);

            // Check for wash sale risk: was the same symbol bought within the last 30 days?
            var washSaleRisk = recentTrades.Any(t =>
                string.Equals(t.Symbol, position.Symbol, StringComparison.OrdinalIgnoreCase) &&
                t.Side == TradeSide.Buy &&
                t.CreatedUtc >= washSaleCutoff);

            var benefit = washSaleRisk ? 0 : loss * EstimatedTaxRate;

            report.Candidates.Add(new HarvestingCandidate
            {
                Symbol = position.Symbol,
                Quantity = position.Quantity,
                AverageCost = position.AverageCost,
                CurrentPrice = position.CurrentPrice.Value,
                UnrealizedLoss = loss,
                EstimatedTaxBenefit = Math.Round(benefit, 2),
                WashSaleRisk = washSaleRisk
            });

            report.TotalEstimatedLoss += loss;
            report.TotalEstimatedTaxBenefit += benefit;
        }

        report.TotalEstimatedLoss = Math.Round(report.TotalEstimatedLoss, 2);
        report.TotalEstimatedTaxBenefit = Math.Round(report.TotalEstimatedTaxBenefit, 2);

        return report;
    }
}
