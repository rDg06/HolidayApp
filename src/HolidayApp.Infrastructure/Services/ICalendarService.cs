using HolidayApp.Shared;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Casos de uso da aplicação. Os endpoints da API só falam com esta interface.</summary>
public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(int year, CancellationToken ct = default);
    Task<CalendarEventDto?> GetEventAsync(Guid id, CancellationToken ct = default);
    Task<CalendarEventDto> CreateEventAsync(SaveCalendarEventRequest request, CancellationToken ct = default);
    Task<CalendarEventDto?> UpdateEventAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct = default);
    Task<bool> DeleteEventAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default);
    Task<YearSummaryDto> GetSummaryAsync(int year, CancellationToken ct = default);

    Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<UserSettingsDto> UpdateSettingsAsync(UserSettingsDto settings, CancellationToken ct = default);
}
