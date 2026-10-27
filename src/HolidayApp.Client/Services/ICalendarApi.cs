using HolidayApp.Shared;

namespace HolidayApp.Client.Services;

/// <summary>Cliente da API. As páginas só dependem desta interface.</summary>
public interface ICalendarApi
{
    Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(int year, CancellationToken ct = default);
    Task<YearSummaryDto> GetSummaryAsync(int year, CancellationToken ct = default);

    Task<CalendarEventDto> CreateEventAsync(SaveCalendarEventRequest request, CancellationToken ct = default);
    Task<CalendarEventDto> UpdateEventAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct = default);
    Task DeleteEventAsync(Guid id, CancellationToken ct = default);

    Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<UserSettingsDto> SaveSettingsAsync(UserSettingsDto settings, CancellationToken ct = default);
}

/// <summary>Erro devolvido pela API, já com uma mensagem legível.</summary>
public sealed class ApiException(string message) : Exception(message);
