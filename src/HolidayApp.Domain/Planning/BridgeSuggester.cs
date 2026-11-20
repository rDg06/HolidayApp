using HolidayApp.Domain.Holidays;
using HolidayApp.Shared.Calendar;

namespace HolidayApp.Domain.Planning;

public sealed record BridgeSuggestion(Holiday Holiday, DateOnly DayOff, int ConsecutiveDaysOff);

/// <summary>
/// Finds "bridges": holidays on a Tuesday or Thursday where taking the Monday
/// or the Friday off is enough for a long weekend.

/// </summary>
public static class BridgeSuggester
{
    public static IReadOnlyList<BridgeSuggestion> Suggest(IReadOnlyList<Holiday> holidays)
    {
        var holidayDates = holidays.Select(h => h.Date).ToHashSet();
        var suggestions = new List<BridgeSuggestion>();

        foreach (var holiday in holidays.OrderBy(h => h.Date))
        {
            DateOnly? candidate = holiday.Date.DayOfWeek switch
            {
                DayOfWeek.Tuesday => holiday.Date.AddDays(-1),
                DayOfWeek.Thursday => holiday.Date.AddDays(1),
                _ => null,
            };

            if (candidate is not { } dayOff || holidayDates.Contains(dayOff))
                continue;

            if (suggestions.Any(s => s.DayOff == dayOff))
                continue;

            suggestions.Add(new BridgeSuggestion(holiday, dayOff, CountDaysOffInARow(dayOff, holidayDates)));
        }

        return suggestions;
    }

    private static int CountDaysOffInARow(DateOnly dayOff, HashSet<DateOnly> holidays)
    {
        bool IsOff(DateOnly d) => d == dayOff || WorkdayCalculator.IsWeekend(d) || holidays.Contains(d);

        var start = dayOff;
        while (IsOff(start.AddDays(-1)))
            start = start.AddDays(-1);

        var end = dayOff;
        while (IsOff(end.AddDays(1)))
            end = end.AddDays(1);

        return end.DayNumber - start.DayNumber + 1;
    }
}
