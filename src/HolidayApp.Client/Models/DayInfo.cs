using HolidayApp.Shared;

namespace HolidayApp.Client.Models;

/// <summary>Tudo o que acontece num dia: feriado e/ou marcações.</summary>
public sealed class DayInfo
{
    public HolidayDto? Holiday { get; set; }
    public List<CalendarEventDto> Events { get; } = [];
}

/// <summary>Informação do rato ao passar por cima de um dia (para arrastar seleções).</summary>
public readonly record struct DayPointer(DateOnly Date, long Buttons);
