using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// CRUD and scheduling logic for dollar-cost averaging schedules.
/// </summary>
public class DcaService(MacroSutraCosmo cosmo)
{
    public Task<DcaSchedule> CreateScheduleAsync(DcaSchedule schedule)
    {
        schedule.NextExecutionUtc = ComputeNextExecutionUtc(schedule, DateTime.UtcNow);
        return cosmo.CreateDcaScheduleAsync(schedule);
    }

    public Task<DcaSchedule?> GetScheduleAsync(string id, string accountId) =>
        cosmo.GetDcaScheduleAsync(id, accountId);

    public Task<List<DcaSchedule>> GetSchedulesAsync(string accountId) =>
        cosmo.GetDcaSchedulesAsync(accountId);

    public Task<DcaSchedule> UpdateScheduleAsync(DcaSchedule schedule)
    {
        schedule.NextExecutionUtc = ComputeNextExecutionUtc(schedule, DateTime.UtcNow);
        return cosmo.UpdateDcaScheduleAsync(schedule);
    }

    public Task DeleteScheduleAsync(string id, string accountId) =>
        cosmo.DeleteDcaScheduleAsync(id, accountId);

    public Task ActivateScheduleAsync(DcaSchedule schedule)
    {
        schedule.IsActive = true;
        schedule.NextExecutionUtc = ComputeNextExecutionUtc(schedule, DateTime.UtcNow);
        return cosmo.UpdateDcaScheduleAsync(schedule);
    }

    public Task DeactivateScheduleAsync(DcaSchedule schedule)
    {
        schedule.IsActive = false;
        return cosmo.UpdateDcaScheduleAsync(schedule);
    }

    /// <summary>
    /// Computes the next execution UTC time based on the schedule frequency.
    /// </summary>
    internal static DateTime ComputeNextExecutionUtc(DcaSchedule schedule, DateTime fromUtc)
    {
        var date = DateOnly.FromDateTime(fromUtc);

        switch (schedule.Frequency)
        {
            case DcaFrequency.Daily:
                // Next trading day at execution time
                date = date.AddDays(1);
                break;

            case DcaFrequency.Weekly:
                var targetDow = schedule.DayOfWeek ?? System.DayOfWeek.Monday;
                var daysUntil = ((int)targetDow - (int)date.DayOfWeek + 7) % 7;
                if (daysUntil == 0) daysUntil = 7; // Always schedule for next occurrence
                date = date.AddDays(daysUntil);
                break;

            case DcaFrequency.BiWeekly:
                var biTargetDow = schedule.DayOfWeek ?? System.DayOfWeek.Monday;
                var biDaysUntil = ((int)biTargetDow - (int)date.DayOfWeek + 7) % 7;
                if (biDaysUntil == 0) biDaysUntil = 7;
                date = date.AddDays(biDaysUntil + 7); // Two weeks out
                break;

            case DcaFrequency.Monthly:
                var targetDay = Math.Min(schedule.DayOfMonth ?? 1, DateTime.DaysInMonth(date.Year, date.Month));
                if (date.Day >= targetDay)
                {
                    date = date.AddMonths(1);
                    targetDay = Math.Min(schedule.DayOfMonth ?? 1, DateTime.DaysInMonth(date.Year, date.Month));
                }
                date = new DateOnly(date.Year, date.Month, targetDay);
                break;
        }

        return date.ToDateTime(schedule.ExecutionTime, DateTimeKind.Utc);
    }
}
