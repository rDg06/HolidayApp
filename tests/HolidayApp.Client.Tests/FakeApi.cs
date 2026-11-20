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
            .. MoreVacations,
        ]);
    public Task<VacationPlanDto> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UserSettingsDto> SaveSettingsAsync(UserSettingsDto settings, CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>Signed-in account.</summary>
    public AccountDto? Me { get; set; }

    public Task<AccountDto> ChangeUsernameAsync(string username, CancellationToken ct = default)
    {
        Me = Me! with { Username = username.Trim().TrimStart('@').ToLowerInvariant() };
        return Task.FromResult(Me);
    }

    /// <summary>Who sees the trip: by default mine, private, nobody added (tests can change it).</summary>
    public TripSharingDto Sharing { get; set; } = new(TripRole.Owner, false, new PersonDto("ana", "Ana"), []);

    /// <summary>Trips on the feed (tests can change the list).</summary>
    public List<FeedTripDto> FeedTrips { get; } = [];

    /// <summary>Trips per feed page (tests can make it small to try "Show older trips").</summary>
    public int FeedPageSize { get; set; } = 10;

    /// <summary>Comments by trip, as the server has them.</summary>
    public Dictionary<Guid, List<TripCommentDto>> Comments { get; } = [];

    /// <summary>Last like sent (trip, liked), and whether the server refuses it.</summary>
    public (Guid Trip, bool Like)? LastLike { get; private set; }
    public bool FailLikes { get; set; }

    /// <summary>Extra trips on "Vacations" (e.g. ones friends added me to).</summary>
    public List<VacationSummaryDto> MoreVacations { get; } = [];

    public Task<TripSharingDto> GetSharingAsync(Guid eventId, CancellationToken ct = default) => Task.FromResult(Sharing);

    public Task<TripSharingDto> SetTripPublicAsync(Guid eventId, bool isPublic, CancellationToken ct = default)
    {
        Sharing = Sharing with { IsPublic = isPublic };
        return Task.FromResult(Sharing);
    }

    public Task<TripSharingDto> AddTripMemberAsync(Guid eventId, string person, bool canEdit, CancellationToken ct = default)
    {
        if (person.Contains("nobody"))
            throw new ApiException("There is no account with that email or user name.");
        var username = person.Split('@')[0].TrimStart('@');
        Sharing = Sharing with { Members = [.. Sharing.Members, new TripMemberDto(username, username.ToUpperInvariant()[..1] + username[1..], canEdit)] };
        return Task.FromResult(Sharing);
    }

    public Task<TripSharingDto> SetTripMemberAsync(Guid eventId, string username, bool canEdit, CancellationToken ct = default)
    {
        Sharing = Sharing with { Members = Sharing.Members.Select(m => m.Username == username ? m with { CanEdit = canEdit } : m).ToList() };
        return Task.FromResult(Sharing);
    }

    public Task RemoveTripMemberAsync(Guid eventId, string username, CancellationToken ct = default)
    {
        Sharing = Sharing with { Members = Sharing.Members.Where(m => m.Username != username).ToList() };
        return Task.CompletedTask;
    }

    public Task<FeedPageDto> GetFeedAsync(int skip, CancellationToken ct = default) =>
        Task.FromResult(new FeedPageDto(FeedTrips.Skip(skip).Take(FeedPageSize).ToList(), FeedTrips.Count > skip + FeedPageSize));

    public Task<TripLikesDto> SetLikeAsync(Guid eventId, bool like, CancellationToken ct = default)
    {
        if (FailLikes)
            throw new ApiException("No connection.");
        LastLike = (eventId, like);
        var trip = FeedTrips.Single(t => t.Id == eventId);
        var others = trip.Likes - (trip.LikedByMe ? 1 : 0);
        return Task.FromResult(new TripLikesDto(others + (like ? 1 : 0), like));
    }

    public Task<IReadOnlyList<TripCommentDto>> GetCommentsAsync(Guid eventId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TripCommentDto>>(Comments.GetValueOrDefault(eventId)?.ToList() ?? []);

    public Task<TripCommentDto> AddCommentAsync(Guid eventId, string text, CancellationToken ct = default)
    {
        var comment = new TripCommentDto(Guid.NewGuid(), new PersonDto("ana", "Ana"), text, DateTime.UtcNow, true);
        (Comments.TryGetValue(eventId, out var list) ? list : Comments[eventId] = []).Add(comment);
        return Task.FromResult(comment);
    }

    public Task DeleteCommentAsync(Guid eventId, Guid commentId, CancellationToken ct = default)
    {
        Comments.GetValueOrDefault(eventId)?.RemoveAll(c => c.Id == commentId);
        return Task.CompletedTask;
    }
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
