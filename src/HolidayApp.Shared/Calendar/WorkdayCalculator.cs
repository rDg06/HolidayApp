namespace HolidayApp.Shared.Calendar;

/// <summary>Regras de dias úteis (segunda a sexta, excluindo feriados).</summary>
public static class WorkdayCalculator
{
    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static bool IsWorkday(DateOnly date, IReadOnlySet<DateOnly> holidays) =>
        !IsWeekend(date) && !holidays.Contains(date);

    /// <summary>Conta os dias úteis entre duas datas (inclusive).</summary>
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
