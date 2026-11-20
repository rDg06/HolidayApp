using HolidayApp.Shared;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Application use cases. The API endpoints only talk to this interface.</summary>
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
