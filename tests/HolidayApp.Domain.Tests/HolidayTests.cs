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
    public void Calcula_o_domingo_de_pascoa(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), EasterCalculator.GetEasterSunday(year));
    }
}

public class PortugueseHolidayProviderTests
{
    private readonly PortugueseHolidayProvider _provider = new();

    [Fact]
    public void Devolve_os_13_feriados_nacionais()
    {
        var holidays = _provider.GetHolidays(2026);

        Assert.Equal(13, holidays.Count);
        Assert.All(holidays, h => Assert.Equal(HolidayKind.Nacional, h.Kind));
    }

    [Fact]
    public void Calcula_os_feriados_moveis_de_2026()
    {
        var holidays = _provider.GetHolidays(2026);

        Assert.Contains(holidays, h => h.Date == new DateOnly(2026, 4, 3) && h.Name == "Sexta-feira Santa");
        Assert.Contains(holidays, h => h.Date == new DateOnly(2026, 6, 4) && h.Name == "Corpo de Deus");
    }

    [Fact]
    public void Inclui_carnaval_e_feriado_municipal_quando_pedido()
    {
        var options = new HolidayOptions(IncludeCarnival: true, MunicipalHolidayName: "Feriado da terra", MunicipalHolidayMonth: 6, MunicipalHolidayDay: 13);

        var holidays = _provider.GetHolidays(2026, options);

        Assert.Contains(holidays, h => h.Date == new DateOnly(2026, 2, 17) && h.Kind == HolidayKind.Facultativo);
        Assert.Contains(holidays, h => h.Date == new DateOnly(2026, 6, 13) && h.Kind == HolidayKind.Municipal);
    }
}

public class BridgeSuggesterTests
{
    [Fact]
    public void Sugere_as_pontes_de_2026()
    {
        var holidays = new PortugueseHolidayProvider().GetHolidays(2026);

        var bridges = BridgeSuggester.Suggest(holidays);

        var expected = new[]
        {
            new DateOnly(2026, 1, 2),   // Ano Novo (quinta) → sexta
            new DateOnly(2026, 6, 5),   // Corpo de Deus (quinta) → sexta
            new DateOnly(2026, 11, 30), // Restauração (terça) → segunda
            new DateOnly(2026, 12, 7),  // Imaculada Conceição (terça) → segunda
        };
        Assert.Equal(expected, bridges.Select(b => b.DayOff).ToArray());
        Assert.All(bridges, b => Assert.Equal(4, b.ConsecutiveDaysOff));
    }
}

public class WorkdayCalculatorTests
{
    [Fact]
    public void Nao_conta_fins_de_semana_nem_feriados()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 12, 25) };

        // 21 a 31 de dezembro de 2026: 9 dias de semana, menos o Natal
        var workdays = WorkdayCalculator.CountWorkdays(new DateOnly(2026, 12, 21), new DateOnly(2026, 12, 31), holidays);

        Assert.Equal(8, workdays);
    }

    [Fact]
    public void Devolve_zero_quando_o_fim_e_anterior_ao_inicio()
    {
        Assert.Equal(0, WorkdayCalculator.CountWorkdays(new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 1), new HashSet<DateOnly>()));
    }
}
