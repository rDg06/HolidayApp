using HolidayApp.Shared;

namespace HolidayApp.Domain.Holidays;

/// <summary>Feriados obrigatórios em Portugal, mais Carnaval e feriado municipal opcionais.</summary>
public sealed class PortugueseHolidayProvider : IHolidayProvider
{
    public IReadOnlyList<Holiday> GetHolidays(int year, HolidayOptions? options = null)
    {
        options ??= HolidayOptions.Default;
        var easter = EasterCalculator.GetEasterSunday(year);

        var holidays = new List<Holiday>
        {
            National(new DateOnly(year, 1, 1), "Ano Novo"),
            National(easter.AddDays(-2), "Sexta-feira Santa"),
            National(easter, "Domingo de Páscoa"),
            National(new DateOnly(year, 4, 25), "Dia da Liberdade"),
            National(new DateOnly(year, 5, 1), "Dia do Trabalhador"),
            National(easter.AddDays(60), "Corpo de Deus"),
            National(new DateOnly(year, 6, 10), "Dia de Portugal"),
            National(new DateOnly(year, 8, 15), "Assunção de Nossa Senhora"),
            National(new DateOnly(year, 10, 5), "Implantação da República"),
            National(new DateOnly(year, 11, 1), "Dia de Todos-os-Santos"),
            National(new DateOnly(year, 12, 1), "Restauração da Independência"),
            National(new DateOnly(year, 12, 8), "Imaculada Conceição"),
            National(new DateOnly(year, 12, 25), "Natal"),
        };

        if (options.IncludeCarnival)
            holidays.Add(new Holiday(easter.AddDays(-47), "Carnaval", HolidayKind.Facultativo));

        if (options.MunicipalHolidayMonth is int month and >= 1 and <= 12
            && options.MunicipalHolidayDay is int day and >= 1)
        {
            var safeDay = Math.Min(day, DateTime.DaysInMonth(year, month)); // 29/02 em anos não bissextos
            var name = string.IsNullOrWhiteSpace(options.MunicipalHolidayName)
                ? "Feriado municipal"
                : options.MunicipalHolidayName.Trim();

            holidays.Add(new Holiday(new DateOnly(year, month, safeDay), name, HolidayKind.Municipal));
        }

        return holidays.OrderBy(h => h.Date).ToList();
    }

    private static Holiday National(DateOnly date, string name) => new(date, name, HolidayKind.Nacional);
}
