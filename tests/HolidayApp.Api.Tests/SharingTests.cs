using System.Net;
using System.Net.Http.Json;
using HolidayApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Api.Tests;

/// <summary>Trips are private by default; the owner can make them public and add friends (view only or can edit).</summary>
public sealed class SharingTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private HttpClient _ana = default!;
    private HttpClient _rui = default!;
    private HttpClient _eva = default!;

    public async Task InitializeAsync()
    {
        _ana = await _factory.SignedInAsync("ana@example.com", "Ana", "ana");
        _rui = await _factory.SignedInAsync("rui@example.com", "Rui", "rui");
        _eva = await _factory.SignedInAsync("eva@example.com", "Eva", "eva");
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static async Task<CalendarEventDto> CreateTripAsync(HttpClient client, string title = "Rome", EventType type = EventType.Vacation)
    {
        var response = await client.PostAsJsonAsync("api/events",
            new SaveCalendarEventRequest(title, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 7), type, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CalendarEventDto>())!;
    }

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid trip, string person, bool canEdit) =>
        client.PostAsJsonAsync($"api/events/{trip}/sharing/members", new AddTripMemberRequest(person, canEdit));

    private static async Task<List<VacationSummaryDto>> VacationsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<VacationSummaryDto>>("api/vacations"))!;

    [Fact]
    public async Task A_new_trip_is_private_only_the_owner_sees_it()
    {
        var trip = await CreateTripAsync(_ana);

        Assert.Equal(HttpStatusCode.NotFound, (await _rui.GetAsync($"api/events/{trip.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _rui.GetAsync($"api/events/{trip.Id}/plan")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _rui.GetAsync($"api/events/{trip.Id}/sharing")).StatusCode);
        Assert.Empty(await VacationsAsync(_rui));
        Assert.Empty((await _rui.GetFromJsonAsync<List<PublicTripDto>>("api/explore"))!);

        var sharing = (await _ana.GetFromJsonAsync<TripSharingDto>($"api/events/{trip.Id}/sharing"))!;
        Assert.Equal(TripRole.Owner, sharing.MyRole);
        Assert.False(sharing.IsPublic);
    }

    [Fact]
    public async Task People_added_see_the_private_trip_by_email_or_user_name()
    {
        var trip = await CreateTripAsync(_ana);

        (await AddAsync(_ana, trip.Id, "RUI@example.com", canEdit: true)).EnsureSuccessStatusCode();
        var response = await AddAsync(_ana, trip.Id, "@eva", canEdit: false);
        var sharing = (await response.Content.ReadFromJsonAsync<TripSharingDto>())!;

        Assert.Equal([("rui", "Rui", true), ("eva", "Eva", false)], sharing.Members.Select(m => (m.Username, m.Name, m.CanEdit)));

        var ruiTrip = Assert.Single(await VacationsAsync(_rui));
        Assert.Equal((TripRole.Editor, "Ana"), (ruiTrip.Role, ruiTrip.Owner!.Name));
        var evaTrip = Assert.Single(await VacationsAsync(_eva));
        Assert.Equal(TripRole.Viewer, evaTrip.Role);
        Assert.Equal(2, Assert.Single(await VacationsAsync(_ana)).People);

        Assert.Equal("Rome", (await _eva.GetFromJsonAsync<CalendarEventDto>($"api/events/{trip.Id}"))!.Title);
    }

    [Fact]
    public async Task Can_edit_saves_the_plan_view_only_cannot()
    {
        var trip = await CreateTripAsync(_ana);
        await AddAsync(_ana, trip.Id, "rui", canEdit: true);
        await AddAsync(_ana, trip.Id, "eva", canEdit: false);

        var byRui = await _rui.PutAsJsonAsync($"api/events/{trip.Id}/plan", new VacationPlanDto { Destination = "Rome" });
        var byEva = await _eva.PutAsJsonAsync($"api/events/{trip.Id}/plan", new VacationPlanDto { Destination = "Paris" });

        byRui.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, byEva.StatusCode);
        Assert.Equal("Rome", (await _ana.GetFromJsonAsync<VacationPlanDto>($"api/events/{trip.Id}/plan"))!.Destination);
        Assert.Equal("Rome", (await _eva.GetFromJsonAsync<VacationPlanDto>($"api/events/{trip.Id}/plan"))!.Destination);

        // The plan saved by a friend still belongs to the owner (goes away with the owner's trip)
        var owner = await _factory.WithDbAsync(db => db.VacationPlans.IgnoreQueryFilters().Select(p => p.UserId).SingleAsync());
        var anaId = await _factory.WithDbAsync(db => db.Accounts.Where(a => a.Email == "ana@example.com").Select(a => a.Id).SingleAsync());
        Assert.Equal(anaId.ToString(), owner);
    }

    [Fact]
    public async Task Only_the_owner_changes_dates_people_and_visibility_or_deletes()
    {
        var trip = await CreateTripAsync(_ana);
        await AddAsync(_ana, trip.Id, "rui", canEdit: true);

        var rename = await _rui.PutAsJsonAsync($"api/events/{trip.Id}",
            new SaveCalendarEventRequest("Mine now", trip.StartDate, trip.EndDate, EventType.Vacation, null));
        var delete = await _rui.DeleteAsync($"api/events/{trip.Id}");
        var add = await AddAsync(_rui, trip.Id, "eva", canEdit: true);
        var makePublic = await _rui.PutAsJsonAsync($"api/events/{trip.Id}/sharing", new SetTripVisibilityRequest(true));
        var promote = await _rui.PutAsJsonAsync($"api/events/{trip.Id}/sharing/members/rui", new SetTripMemberRequest(true));

        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, makePublic.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        Assert.Equal("Rome", (await _ana.GetFromJsonAsync<CalendarEventDto>($"api/events/{trip.Id}"))!.Title);
    }

    [Fact]
    public async Task Owner_changes_what_someone_can_do_and_removes_them()
    {
        var trip = await CreateTripAsync(_ana);
        await AddAsync(_ana, trip.Id, "rui", canEdit: false);

        (await _ana.PutAsJsonAsync($"api/events/{trip.Id}/sharing/members/rui", new SetTripMemberRequest(true))).EnsureSuccessStatusCode();
        Assert.Equal(TripRole.Editor, Assert.Single(await VacationsAsync(_rui)).Role);

        (await _ana.DeleteAsync($"api/events/{trip.Id}/sharing/members/rui")).EnsureSuccessStatusCode();
        Assert.Empty(await VacationsAsync(_rui));
        Assert.Equal(HttpStatusCode.NotFound, (await _rui.GetAsync($"api/events/{trip.Id}/plan")).StatusCode);
    }

    [Fact]
    public async Task Someone_added_can_leave_but_not_remove_others()
    {
        var trip = await CreateTripAsync(_ana);
        await AddAsync(_ana, trip.Id, "rui", canEdit: true);
        await AddAsync(_ana, trip.Id, "eva", canEdit: true);

        Assert.Equal(HttpStatusCode.Forbidden, (await _rui.DeleteAsync($"api/events/{trip.Id}/sharing/members/eva")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _rui.DeleteAsync($"api/events/{trip.Id}/sharing/members/rui")).StatusCode);

        Assert.Empty(await VacationsAsync(_rui));
        Assert.Single(await VacationsAsync(_eva));
    }

    [Fact]
    public async Task Adding_an_unknown_person_yourself_or_twice_is_rejected()
    {
        var trip = await CreateTripAsync(_ana);
        await AddAsync(_ana, trip.Id, "rui", canEdit: false);

        var unknown = await AddAsync(_ana, trip.Id, "nobody@example.com", canEdit: false);
        var self = await AddAsync(_ana, trip.Id, "ana", canEdit: false);
        var twice = await AddAsync(_ana, trip.Id, "rui@example.com", canEdit: true);

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("no account", await unknown.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
    }

    [Fact]
    public async Task Other_entries_are_never_shared()
    {
        var dentist = await CreateTripAsync(_ana, "Dentist", EventType.Personal);

        Assert.Equal(HttpStatusCode.BadRequest, (await AddAsync(_ana, dentist.Id, "rui", canEdit: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _ana.PutAsJsonAsync($"api/events/{dentist.Id}/sharing", new SetTripVisibilityRequest(true))).StatusCode);
    }

    [Fact]
    public async Task A_public_trip_is_seen_by_everyone_read_only_without_the_people()
    {
        var trip = await CreateTripAsync(_ana);
        await _ana.PutAsJsonAsync($"api/events/{trip.Id}/plan", new VacationPlanDto { Destination = "Rome" });
        await AddAsync(_ana, trip.Id, "rui", canEdit: true);

        (await _ana.PutAsJsonAsync($"api/events/{trip.Id}/sharing", new SetTripVisibilityRequest(true))).EnsureSuccessStatusCode();

        var explore = Assert.Single((await _eva.GetFromJsonAsync<List<PublicTripDto>>("api/explore"))!);
        Assert.Equal(("Rome", "Rome", "ana", false), (explore.Title, explore.Destination, explore.Owner.Username, explore.IsMine));
        Assert.True(Assert.Single((await _ana.GetFromJsonAsync<List<PublicTripDto>>("api/explore"))!).IsMine);

        var sharing = (await _eva.GetFromJsonAsync<TripSharingDto>($"api/events/{trip.Id}/sharing"))!;
        Assert.Equal(TripRole.PublicViewer, sharing.MyRole);
        Assert.Empty(sharing.Members);
        Assert.Equal("Rome", (await _eva.GetFromJsonAsync<VacationPlanDto>($"api/events/{trip.Id}/plan"))!.Destination);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _eva.PutAsJsonAsync($"api/events/{trip.Id}/plan", new VacationPlanDto { Destination = "Mine" })).StatusCode);

        // Public trips are not added to my own list or calendar
        Assert.Empty(await VacationsAsync(_eva));

        (await _ana.PutAsJsonAsync($"api/events/{trip.Id}/sharing", new SetTripVisibilityRequest(false))).EnsureSuccessStatusCode();
        Assert.Empty((await _eva.GetFromJsonAsync<List<PublicTripDto>>("api/explore"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await _eva.GetAsync($"api/events/{trip.Id}/plan")).StatusCode);
    }

    [Fact]
    public async Task Booking_codes_are_hidden_from_strangers_on_a_public_trip()
    {
        var trip = await CreateTripAsync(_ana);
        var plan = new VacationPlanDto
        {
            Bookings = [new PlanBookingDto { Name = "Flight to Rome", Date = new DateOnly(2026, 8, 3), Reference = "ABC123" }],
        };
        (await _ana.PutAsJsonAsync($"api/events/{trip.Id}/plan", plan)).EnsureSuccessStatusCode();
        await AddAsync(_ana, trip.Id, "rui", canEdit: false);
        (await _ana.PutAsJsonAsync($"api/events/{trip.Id}/sharing", new SetTripVisibilityRequest(true))).EnsureSuccessStatusCode();

        async Task<string?> CodeSeenBy(HttpClient client) =>
            Assert.Single((await client.GetFromJsonAsync<VacationPlanDto>($"api/events/{trip.Id}/plan"))!.Bookings).Reference;

        Assert.Equal("ABC123", await CodeSeenBy(_ana));
        Assert.Equal("ABC123", await CodeSeenBy(_rui)); // added to the trip: a friend
        Assert.Null(await CodeSeenBy(_eva));            // only sees it because it is public
    }

    [Fact]
    public async Task Shared_trips_do_not_appear_in_the_friends_calendar()
    {
        var trip = await CreateTripAsync(_ana);
        await AddAsync(_ana, trip.Id, "rui", canEdit: true);

        Assert.Empty((await _rui.GetFromJsonAsync<List<CalendarEventDto>>("api/events?year=2026"))!);
        Assert.Equal(0, (await _rui.GetFromJsonAsync<YearSummaryDto>("api/calendar/2026/summary"))!.UsedVacationDays);
    }

    [Fact]
    public async Task Deleting_the_trip_or_an_account_removes_the_people()
    {
        var trip = await CreateTripAsync(_ana);
        var other = await CreateTripAsync(_rui, "Lisbon");
        await AddAsync(_ana, trip.Id, "rui", canEdit: true);
        await AddAsync(_rui, other.Id, "ana", canEdit: true);

        (await _ana.DeleteAsync($"api/events/{trip.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(["Lisbon"], (await VacationsAsync(_rui)).Select(v => v.Title));

        (await _ana.DeleteAsync("api/me")).EnsureSuccessStatusCode();
        Assert.Equal(0, await _factory.WithDbAsync(db => db.TripMembers.CountAsync()));
        Assert.Equal(0, Assert.Single(await VacationsAsync(_rui)).People);
    }
}
