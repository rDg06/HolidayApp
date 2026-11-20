using HolidayApp.Domain.Holidays;
using HolidayApp.Domain.Planning;
using HolidayApp.Shared;
using HolidayApp.Shared.Calendar;

namespace HolidayApp.Domain.Tests;

public class EasterCalculatorTests
{
    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    public void Calculates_Easter_Sunday(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), EasterCalculator.GetEasterSunday(year));
    }
}

public class PortugueseHolidayProviderTests
{
    private readonly PortugueseHolidayProvider _provider = new();

    [Fact]
    public void Returns_the_13_national_holidays()
    {
        var holidays = _provider.GetHolidays(2026);

        Assert.Equal(13, holidays.Count);
        Assert.All(holidays, h => Assert.Equal(HolidayKind.National, h.Kind));
    }

    [Fact]
    public void Calculates_the_movable_holidays_of_2026()
    {
        var holidays = _provider.GetHolidays(2026);

        Assert.Contains(holidays, h => h.Date == new DateOnly(2026, 4, 3) && h.Name == "Good Friday");
        Assert.Contains(holidays, h => h.Date == new DateOnly(2026, 6, 4) && h.Name == "Corpus Christi");
    }

    [Fact]
    public void Includes_Carnival_and_the_municipal_holiday_when_asked()
    {
        var options = new HolidayOptions(IncludeCarnival: true, MunicipalHolidayName: "Town day", MunicipalHolidayMonth: 6, MunicipalHolidayDay: 13);

        var holidays = _provider.GetHolidays(2026, options);

        Assert.Contains(holidays, h => h.Date == new DateOnly(2026, 2, 17) && h.Kind == HolidayKind.Optional);
        Assert.Contains(holidays, h => h.Date == new DateOnly(2026, 6, 13) && h.Kind == HolidayKind.Municipal);
    }
}

public class BridgeSuggesterTests
{
    [Fact]
    public void Suggests_the_bridges_of_2026()
    {
        var holidays = new PortugueseHolidayProvider().GetHolidays(2026);

        var bridges = BridgeSuggester.Suggest(holidays);

        var expected = new[]
        {
            new DateOnly(2026, 1, 2),   // New Year's Day (Thursday) → Friday
            new DateOnly(2026, 6, 5),   // Corpus Christi (Thursday) → Friday
            new DateOnly(2026, 11, 30), // Restoration (Tuesday) → Monday
            new DateOnly(2026, 12, 7),  // Immaculate Conception (Tuesday) → Monday
        };
        Assert.Equal(expected, bridges.Select(b => b.DayOff).ToArray());
        Assert.All(bridges, b => Assert.Equal(4, b.ConsecutiveDaysOff));
    }
}

public class WorkdayCalculatorTests
{
    [Fact]
    public void Does_not_count_weekends_or_holidays()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 12, 25) };

        // 21 to 31 December 2026: 9 weekdays, minus Christmas
        var workdays = WorkdayCalculator.CountWorkdays(new DateOnly(2026, 12, 21), new DateOnly(2026, 12, 31), holidays);

        Assert.Equal(8, workdays);
    }

    [Fact]
    public void Returns_zero_when_the_end_is_before_the_start()
    {
        Assert.Equal(0, WorkdayCalculator.CountWorkdays(new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 1), new HashSet<DateOnly>()));
    }
}
