using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;

namespace MacroSutra.Tests.Services;

/// <summary>
/// WU5: Dollar-cost averaging scheduling logic tests.
/// Tests use the internal static ComputeNextExecutionUtc method directly.
/// </summary>
public class DcaServiceTests
{
    // Monday 2024-06-10 at 10:00 UTC
    private static readonly DateTime BaseDate = new(2024, 6, 10, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ComputeNextExecutionUtc_Daily_ReturnsNextDay()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Daily,
            ExecutionTime = new TimeOnly(14, 30)
        };
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate);
        Assert.Equal(new DateTime(2024, 6, 11, 14, 30, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void ComputeNextExecutionUtc_Weekly_ReturnsCorrectDay()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Weekly,
            DayOfWeek = DayOfWeek.Wednesday,
            ExecutionTime = new TimeOnly(10, 0)
        };
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate); // Monday → next Wednesday
        Assert.Equal(DayOfWeek.Wednesday, next.DayOfWeek);
        Assert.Equal(new DateTime(2024, 6, 12, 10, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void ComputeNextExecutionUtc_Weekly_SameDaySchedulesNextWeek()
    {
        // BaseDate is Monday; scheduling for Monday should go to next Monday
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Weekly,
            DayOfWeek = DayOfWeek.Monday,
            ExecutionTime = new TimeOnly(10, 0)
        };
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate);
        Assert.Equal(new DateTime(2024, 6, 17, 10, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void ComputeNextExecutionUtc_BiWeekly_ReturnsTwoWeeksOut()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.BiWeekly,
            DayOfWeek = DayOfWeek.Friday,
            ExecutionTime = new TimeOnly(9, 0)
        };
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate);
        Assert.Equal(DayOfWeek.Friday, next.DayOfWeek);
        // Monday → next Friday is +5 days (Jun 15), then +7 = Jun 21
        // Actually: (5 - 1 + 7) % 7 = 4, if 0 then 7, so daysUntil = 5
        // biDaysUntil + 7 = 5 + 7 = 12, Jun 10 + 12 = Jun 22 (Saturday)
        // Wait: DayOfWeek.Friday = 5, Monday = 1: (5 - 1 + 7) % 7 = 11 % 7 = 4, != 0 so biDaysUntil = 4+7=11
        // Jun 10 + 11 = Jun 21 (Friday)
        Assert.Equal(new DateTime(2024, 6, 21, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void ComputeNextExecutionUtc_Monthly_ReturnsCorrectDayOfMonth()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Monthly,
            DayOfMonth = 15,
            ExecutionTime = new TimeOnly(10, 0)
        };
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate); // June 10 → June 15
        Assert.Equal(new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void ComputeNextExecutionUtc_Monthly_PastDay_SkipsToNextMonth()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Monthly,
            DayOfMonth = 5,
            ExecutionTime = new TimeOnly(10, 0)
        };
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate); // June 10, day 5 already passed
        Assert.Equal(new DateTime(2024, 7, 5, 10, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void ComputeNextExecutionUtc_Monthly_Day31_ClampsToEndOfMonth()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Monthly,
            DayOfMonth = 31,
            ExecutionTime = new TimeOnly(10, 0)
        };
        // June has 30 days; day 31 > day 10, so target = min(31, 30) = 30
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate);
        Assert.Equal(new DateTime(2024, 6, 30, 10, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void ComputeNextExecutionUtc_Weekly_DefaultDayIsMonday()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Weekly,
            DayOfWeek = null, // default
            ExecutionTime = new TimeOnly(10, 0)
        };
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate);
        Assert.Equal(DayOfWeek.Monday, next.DayOfWeek);
    }

    [Fact]
    public void ComputeNextExecutionUtc_Monthly_DefaultDayIs1()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Monthly,
            DayOfMonth = null, // defaults to 1
            ExecutionTime = new TimeOnly(10, 0)
        };
        // June 10, day 1 already passed → next month
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate);
        Assert.Equal(1, next.Day);
        Assert.Equal(7, next.Month); // July 1
    }

    [Fact]
    public void ComputeNextExecutionUtc_PreservesExecutionTime()
    {
        var schedule = new DcaSchedule
        {
            Frequency = DcaFrequency.Daily,
            ExecutionTime = new TimeOnly(15, 45, 30)
        };
        var next = DcaService.ComputeNextExecutionUtc(schedule, BaseDate);
        Assert.Equal(15, next.Hour);
        Assert.Equal(45, next.Minute);
        Assert.Equal(30, next.Second);
    }
}
