namespace MacroSutra.Core.Models;

/// <summary>
/// Report of tax-loss harvesting opportunities across portfolio positions.
/// Read-only analysis — does not execute trades.
/// </summary>
public class TaxLossHarvestingReport
{
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
    public decimal TotalEstimatedLoss { get; set; }
    public decimal TotalEstimatedTaxBenefit { get; set; }
    public List<HarvestingCandidate> Candidates { get; set; } = new();
}

/// <summary>
/// A single position eligible for tax-loss harvesting.
/// </summary>
public class HarvestingCandidate
{
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal UnrealizedLoss { get; set; }
    public decimal EstimatedTaxBenefit { get; set; }
    public bool WashSaleRisk { get; set; }
}
