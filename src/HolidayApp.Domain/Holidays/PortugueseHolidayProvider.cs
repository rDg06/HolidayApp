using HolidayApp.Shared;

namespace HolidayApp.Domain.Holidays;

/// <summary>Mandatory public holidays in Portugal, plus optional Carnival and municipal holiday.</summary>
public sealed class PortugueseHolidayProvider : IHolidayProvider
{
    public IReadOnlyList<Holiday> GetHolidays(int year, HolidayOptions? options = null)
    {
        options ??= HolidayOptions.Default;
        var easter = EasterCalculator.GetEasterSunday(year);

        var holidays = new List<Holiday>
        {
            National(new DateOnly(year, 1, 1), "New Year's Day"),
            National(easter.AddDays(-2), "Good Friday"),
            National(easter, "Easter Sunday"),
            National(new DateOnly(year, 4, 25), "Freedom Day"),
            National(new DateOnly(year, 5, 1), "Labour Day"),
            National(easter.AddDays(60), "Corpus Christi"),
            National(new DateOnly(year, 6, 10), "Portugal Day"),
            National(new DateOnly(year, 8, 15), "Assumption Day"),
            National(new DateOnly(year, 10, 5), "Republic Day"),
            National(new DateOnly(year, 11, 1), "All Saints' Day"),
            National(new DateOnly(year, 12, 1), "Restoration of Independence"),
            National(new DateOnly(year, 12, 8), "Immaculate Conception"),
            National(new DateOnly(year, 12, 25), "Christmas Day"),
        };

        if (options.IncludeCarnival)
            holidays.Add(new Holiday(easter.AddDays(-47), "Carnival", HolidayKind.Optional));

        if (options.MunicipalHolidayMonth is int month and >= 1 and <= 12
            && options.MunicipalHolidayDay is int day and >= 1)
        {
            var safeDay = Math.Min(day, DateTime.DaysInMonth(year, month)); // 29/02 in non-leap years
            var name = string.IsNullOrWhiteSpace(options.MunicipalHolidayName)
                ? "Municipal holiday"
                : options.MunicipalHolidayName.Trim();

            holidays.Add(new Holiday(new DateOnly(year, month, safeDay), name, HolidayKind.Municipal));
        }

        return holidays.OrderBy(h => h.Date).ToList();
    }

    private static Holiday National(DateOnly date, string name) => new(date, name, HolidayKind.National);
}
