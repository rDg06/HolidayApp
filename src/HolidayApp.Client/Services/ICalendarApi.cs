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

    /// <summary>The signed-in account.</summary>
    Task<AccountDto?> GetMeAsync(CancellationToken ct = default);

    Task<AccountDto> ChangeUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Who sees a trip (public or private, the people in it) and what I can do.</summary>
    Task<TripSharingDto> GetSharingAsync(Guid eventId, CancellationToken ct = default);
    Task<TripSharingDto> SetTripPublicAsync(Guid eventId, bool isPublic, CancellationToken ct = default);

    /// <summary>Adds someone to the trip by email or user name.</summary>
    Task<TripSharingDto> AddTripMemberAsync(Guid eventId, string person, bool canEdit, CancellationToken ct = default);
    Task<TripSharingDto> SetTripMemberAsync(Guid eventId, string username, bool canEdit, CancellationToken ct = default);

    /// <summary>The owner removes someone, or I leave the trip (my own user name).</summary>
    Task RemoveTripMemberAsync(Guid eventId, string username, CancellationToken ct = default);

    /// <summary>Public trips of everyone ("Explore").</summary>
    Task<IReadOnlyList<PublicTripDto>> GetPublicTripsAsync(CancellationToken ct = default);

    /// <summary>Deletes the account and all its data.</summary>
    Task DeleteMeAsync(CancellationToken ct = default);

    Task<AccountDto> RenameMeAsync(string name, CancellationToken ct = default);
    Task ChangePasswordAsync(string current, string replacement, CancellationToken ct = default);
}

/// <summary>Error returned by the API, already with a readable message.</summary>
public sealed class ApiException(string message) : Exception(message);
