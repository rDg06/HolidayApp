using HolidayApp.Shared;

namespace HolidayApp.Client.Services;

/// <summary>API client. The pages only depend on this interface.</summary>
public interface ICalendarApi
{
    Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(int year, CancellationToken ct = default);
    Task<YearSummaryDto> GetSummaryAsync(int year, CancellationToken ct = default);

    Task<CalendarEventDto> GetEventAsync(Guid id, CancellationToken ct = default);
    Task<CalendarEventDto> CreateEventAsync(SaveCalendarEventRequest request, CancellationToken ct = default);
    Task<CalendarEventDto> UpdateEventAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct = default);
    Task DeleteEventAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default);
    Task<VacationPlanDto> GetPlanAsync(Guid eventId, CancellationToken ct = default);
    Task<VacationPlanDto> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default);

    Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<UserSettingsDto> SaveSettingsAsync(UserSettingsDto settings, CancellationToken ct = default);

    Task<IReadOnlyList<VisitedPlaceDto>> GetVisitedPlacesAsync(CancellationToken ct = default);
    Task<VisitedPlaceDto> AddVisitedPlaceAsync(SaveVisitedPlaceRequest request, CancellationToken ct = default);
    Task DeleteVisitedPlaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>The signed-in account, or null without accounts (home mode).</summary>
    Task<AccountDto?> GetMeAsync(CancellationToken ct = default);

    /// <summary>Deletes the account and all its data.</summary>
    Task DeleteMeAsync(CancellationToken ct = default);

    Task<AccountDto> RenameMeAsync(string name, CancellationToken ct = default);
    Task ChangePasswordAsync(string current, string replacement, CancellationToken ct = default);
}

/// <summary>Error returned by the API, already with a readable message.</summary>
public sealed class ApiException(string message) : Exception(message);
