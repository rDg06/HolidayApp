using HolidayApp.Shared;

namespace HolidayApp.Domain.Holidays;

public sealed record Holiday(DateOnly Date, string Name, HolidayKind Kind);

/// <summary>Opções que alteram a lista de feriados de um utilizador.</summary>
public sealed record HolidayOptions(
    bool IncludeCarnival = false,
    string? MunicipalHolidayName = null,
    int? MunicipalHolidayMonth = null,
    int? MunicipalHolidayDay = null)
{
    public static HolidayOptions Default { get; } = new();
}

/// <summary>
/// Fornece os feriados de um ano. Para suportar outro país basta criar
/// outra implementação (ex.: SpanishHolidayProvider).
/// </summary>
public interface IHolidayProvider
{
    IReadOnlyList<Holiday> GetHolidays(int year, HolidayOptions? options = null);
}
