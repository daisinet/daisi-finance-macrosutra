using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A dollar-cost averaging schedule that places recurring buy orders.
/// Stored in the DcaSchedules container, partitioned by AccountId.
/// </summary>
public class DcaSchedule
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(DcaSchedule);
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string BrokerageAccountId { get; set; } = "";
    public DcaFrequency Frequency { get; set; } = DcaFrequency.Weekly;
    public DayOfWeek? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeOnly ExecutionTime { get; set; } = new(10, 0);
    public decimal InvestmentAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastExecutedUtc { get; set; }
    public DateTime? NextExecutionUtc { get; set; }
    public int TotalExecutions { get; set; }
    public decimal TotalInvested { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
