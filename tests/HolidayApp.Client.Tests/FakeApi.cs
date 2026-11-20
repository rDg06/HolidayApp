using HolidayApp.Client.Services;

namespace HolidayApp.Client.Tests;

/// <summary>Fake API with a trip to Rome (2–4 Nov 2026), for the page tests.</summary>
public sealed class FakeApi(Guid tripId) : ICalendarApi
{
    public Task<CalendarEventDto> GetEventAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(new CalendarEventDto(tripId, "Roma", Nov(2), Nov(4), EventType.Vacation, null));

    public Task<VacationPlanDto> GetPlanAsync(Guid eventId, CancellationToken ct = default) =>
        Task.FromResult(new VacationPlanDto
        {
            Destination = "Roma",
            Bookings =
            [
                new PlanBookingDto { Type = BookingType.Flight, Name = "Lisbon-Rome", Date = Nov(2), Reference = "TP832", Cost = 189.90m },
                new PlanBookingDto { Type = BookingType.Lodging, Name = "Hotel Trevi", Date = Nov(3) },
            ],
            Tasks = [new PlanTaskDto { Text = "Suitcases", Done = true }, new PlanTaskDto { Text = "Tickets" }],
            Places =
            [
                new PlanPlaceDto { Name = "Hotel Roma", Kind = PlaceKind.Lodging, Date = Nov(2), EndDate = Nov(3), Latitude = 41.9, Longitude = 12.49 },
                new PlanPlaceDto { Name = "Colosseum", Date = Nov(3), Latitude = 41.89, Longitude = 12.49 },
                new PlanPlaceDto { Name = "Metro Colosseo", Kind = PlaceKind.Metro, Latitude = 41.891, Longitude = 12.4923 },
            ],
        });

    public Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<YearSummaryDto> GetSummaryAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CalendarEventDto> CreateEventAsync(SaveCalendarEventRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CalendarEventDto> UpdateEventAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteEventAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VacationSummaryDto>>(
        [
            // 2–6 Nov 2026: 5 days, 4 working (holiday or bridge aside); 3 places to visit, 1 visited
            new VacationSummaryDto(tripId, "Roma", Nov(2), Nov(6), 4, "Roma", 2, 3, 1, 2, 1, 500m, 557.90m),
        ]);
    public Task<VacationPlanDto> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UserSettingsDto> SaveSettingsAsync(UserSettingsDto settings, CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>Signed-in account (null = home mode, no accounts).</summary>
    public AccountDto? Me { get; set; }
    public bool MeDeleted { get; private set; }

    public Task<AccountDto?> GetMeAsync(CancellationToken ct = default) => Task.FromResult(Me);

    public Task DeleteMeAsync(CancellationToken ct = default)
    {
        MeDeleted = true;
        return Task.CompletedTask;
    }

    public Task<AccountDto> RenameMeAsync(string name, CancellationToken ct = default)
    {
        Me = Me! with { Name = name.Trim() };
        return Task.FromResult(Me);
    }

    /// <summary>Last new password asked for (null if never).</summary>
    public string? ChangedPassword { get; private set; }

    public Task ChangePasswordAsync(string current, string replacement, CancellationToken ct = default)
    {
        if (current != "right-current")
            throw new ApiException("The current password is wrong.");
        ChangedPassword = replacement;
        return Task.CompletedTask;
    }

    /// <summary>Places added by hand (tests can change the list).</summary>
    public List<VisitedPlaceDto> Visited { get; } = [];

    public Task<IReadOnlyList<VisitedPlaceDto>> GetVisitedPlacesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VisitedPlaceDto>>(Visited.ToList());

    public Task<VisitedPlaceDto> AddVisitedPlaceAsync(SaveVisitedPlaceRequest request, CancellationToken ct = default)
    {
        var place = new VisitedPlaceDto(Guid.NewGuid(), request.Name, request.Latitude, request.Longitude, request.Country, request.Year);
        Visited.Add(place);
        return Task.FromResult(place);
    }

    public Task DeleteVisitedPlaceAsync(Guid id, CancellationToken ct = default)
    {
        Visited.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }

    private static DateOnly Nov(int day) => new(2026, 11, day);
}
