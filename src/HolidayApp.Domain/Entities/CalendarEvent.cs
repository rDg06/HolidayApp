using HolidayApp.Shared;

namespace HolidayApp.Domain.Entities;

/// <summary>Uma marcação no calendário (férias, compromisso pessoal, etc.).</summary>
public sealed class CalendarEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Dono da marcação. Preparado para vários utilizadores.</summary>
    public required string UserId { get; set; }

    public required string Title { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public EventType Type { get; set; } = EventType.Ferias;
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
