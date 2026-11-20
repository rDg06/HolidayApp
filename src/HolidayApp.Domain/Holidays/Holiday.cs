using HolidayApp.Shared;

namespace HolidayApp.Domain.Holidays;

public sealed record Holiday(DateOnly Date, string Name, HolidayKind Kind);

/// <summary>Options that change a user's list of holidays.</summary>
public sealed record HolidayOptions(
    bool IncludeCarnival = false,
    string? MunicipalHolidayName = null,
    int? MunicipalHolidayMonth = null,
    int? MunicipalHolidayDay = null)
{
    public static HolidayOptions Default { get; } = new();
}

/// <summary>
/// Provides the holidays of a year. To support another country just add
/// another implementation (e.g. SpanishHolidayProvider).
/// </summary>
public interface IHolidayProvider
{
    IReadOnlyList<Holiday> GetHolidays(int year, HolidayOptions? options = null);
}
