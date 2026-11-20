namespace HolidayApp.Shared.Calendar;

/// <summary>Working-day rules (Monday to Friday, excluding public holidays).</summary>
public static class WorkdayCalculator
{
    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static bool IsWorkday(DateOnly date, IReadOnlySet<DateOnly> holidays) =>
        !IsWeekend(date) && !holidays.Contains(date);

    /// <summary>Counts the working days between two dates (inclusive).</summary>
    public static int CountWorkdays(DateOnly start, DateOnly end, IReadOnlySet<DateOnly> holidays)
    {
        if (end < start)
            return 0;

        var count = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (IsWorkday(day, holidays))
                count++;
        }

        return count;
    }
}
