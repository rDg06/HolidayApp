namespace HolidayApp.Shared;

/// <summary>Marcação devolvida pela API.</summary>
public sealed record CalendarEventDto(
    Guid Id,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    EventType Type,
    string? Notes);

/// <summary>Pedido para criar ou atualizar uma marcação.</summary>
public sealed record SaveCalendarEventRequest(
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    EventType Type,
    string? Notes);

/// <summary>Feriado num determinado dia.</summary>
public sealed record HolidayDto(DateOnly Date, string Name, HolidayKind Kind);

/// <summary>Sugestão de "ponte": um dia de férias que, junto a um feriado, dá um fim de semana prolongado.</summary>
public sealed record BridgeSuggestionDto(
    DateOnly HolidayDate,
    string HolidayName,
    DateOnly SuggestedDayOff,
    int ConsecutiveDaysOff,
    bool AlreadyBooked);

/// <summary>Resumo anual do saldo de férias.</summary>
public sealed record YearSummaryDto(
    int Year,
    int AnnualVacationDays,
    int UsedVacationDays,
    int RemainingVacationDays,
    IReadOnlyList<BridgeSuggestionDto> Bridges);

/// <summary>Preferências do utilizador (editáveis na página de definições).</summary>
public sealed class UserSettingsDto
{
    public int AnnualVacationDays { get; set; } = 22;
    public bool IncludeCarnival { get; set; }
    public string? MunicipalHolidayName { get; set; }
    public int? MunicipalHolidayMonth { get; set; }
    public int? MunicipalHolidayDay { get; set; }
}
