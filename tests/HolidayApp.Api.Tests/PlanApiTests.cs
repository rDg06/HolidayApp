using System.Net;
using System.Net.Http.Json;
using HolidayApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Api.Tests;

public sealed class PlanApiTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private HttpClient _client = default!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<CalendarEventDto> CreateVacationAsync(string start = "2026-08-03", string end = "2026-08-14")
    {
        var response = await _client.PostAsJsonAsync("api/events",
            new SaveCalendarEventRequest("Algarve", DateOnly.Parse(start), DateOnly.Parse(end), EventType.Vacation, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CalendarEventDto>())!;
    }

    [Fact]
    public async Task Saves_and_returns_the_cleaned_plan()
    {
        var ev = await CreateVacationAsync();
        var plan = new VacationPlanDto
        {
            Destination = "  Algarve  ",
            Days =
            [
                new PlanDayDto { Date = new DateOnly(2026, 8, 3), Place = " Faro " },
                new PlanDayDto { Date = new DateOnly(2026, 8, 4) }, // empty: not saved
            ],
            Tasks = [new PlanTaskDto { Text = " Suitcases " }],
            RouteMode = RouteMode.Car,
        };

        var saved = await (await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", plan)).Content.ReadFromJsonAsync<VacationPlanDto>();
        var reloaded = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");

        Assert.Equal("Algarve", saved!.Destination);
        Assert.Equal("Algarve", reloaded!.Destination);
        Assert.Equal("Faro", Assert.Single(reloaded.Days).Place);
        Assert.Equal("Suitcases", Assert.Single(reloaded.Tasks).Text);
        Assert.Equal(RouteMode.Car, reloaded.RouteMode);
    }

    [Theory]
    [InlineData("""{"bookings":[null]}""")]
    [InlineData("""{"tasks":[null],"places":[null]}""")]
    public async Task Empty_items_return_400_not_500(string body)
    {
        var ev = await CreateVacationAsync();

        var response = await _client.PutAsync($"api/events/{ev.Id}/plan",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("9999-12-31")]
    [InlineData("0001-01-01")]
    public async Task Entries_outside_the_supported_years_are_rejected(string date)
    {
        var response = await _client.PostAsJsonAsync("api/events",
            new SaveCalendarEventRequest("x", DateOnly.Parse(date), DateOnly.Parse(date), EventType.Vacation, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Booking_outside_the_vacation_dates_is_rejected()
    {
        var ev = await CreateVacationAsync();
        var plan = new VacationPlanDto { Bookings = [new PlanBookingDto { Name = "Hotel", Date = new DateOnly(2026, 9, 1) }] };

        var response = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", plan);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overwriting_someone_else_is_rejected()
    {
        var ev = await CreateVacationAsync();

        // Two people open the same trip (no plan yet: version 0)
        var ana = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");
        var rui = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");
        Assert.Equal(0, ana!.Version);

        ana.Destination = "Algarve";
        var anaSaves = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", ana);
        rui!.Destination = "Azores";
        var ruiSaves = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", rui);

        Assert.Equal(HttpStatusCode.OK, anaSaves.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ruiSaves.StatusCode);
        Assert.Contains("Someone else", await ruiSaves.Content.ReadAsStringAsync());
        Assert.Equal("Algarve", (await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan"))!.Destination);
    }

    [Fact]
    public async Task Saving_with_the_returned_version_keeps_working()
    {
        var ev = await CreateVacationAsync();
        var plan = (await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan"))!;

        plan.Destination = "Algarve";
        var first = (await (await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", plan)).Content.ReadFromJsonAsync<VacationPlanDto>())!;
        first.Destination = "Lagos";
        var second = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", first);

        Assert.NotEqual(0, first.Version);
        Assert.True(first.Version < 9_007_199_254_740_991); // fits in a JavaScript number without rounding
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Summary_has_one_point_per_lodging_or_the_centre_of_the_places()
    {
        var byCity = await CreateVacationAsync();
        await _client.PutAsJsonAsync($"api/events/{byCity.Id}/plan", new VacationPlanDto
        {
            Places =
            [
                new PlanPlaceDto { Name = "Hotel Paris", Kind = PlaceKind.Lodging, Date = new DateOnly(2026, 8, 8), EndDate = new DateOnly(2026, 8, 14), Latitude = 48.86, Longitude = 2.35 },
                new PlanPlaceDto { Name = "Hotel Roma", Kind = PlaceKind.Lodging, Date = new DateOnly(2026, 8, 3), EndDate = new DateOnly(2026, 8, 8), Latitude = 41.9, Longitude = 12.49 },
                new PlanPlaceDto { Name = "Colosseum", Latitude = 41.89, Longitude = 12.49 },
            ],
        });
        var placesOnly = await CreateVacationAsync("2026-09-01", "2026-09-03");
        await _client.PutAsJsonAsync($"api/events/{placesOnly.Id}/plan", new VacationPlanDto
        {
            Destination = "Lisbon",
            Places = [new PlanPlaceDto { Name = "A", Latitude = 38.70, Longitude = -9.10 }, new PlanPlaceDto { Name = "B", Latitude = 38.72, Longitude = -9.14 }],
        });
        var nothing = await CreateVacationAsync("2026-10-01", "2026-10-02");

        var summaries = (await _client.GetFromJsonAsync<List<VacationSummaryDto>>("api/vacations"))!.ToDictionary(v => v.Id);

        // In order of the stay: Rome, then Paris
        Assert.Equal(["Hotel Roma", "Hotel Paris"], summaries[byCity.Id].Points!.Select(p => p.Name));
        var centre = Assert.Single(summaries[placesOnly.Id].Points!);
        Assert.Equal(("Lisbon", 38.71, -9.12), (centre.Name, Math.Round(centre.Latitude, 2), Math.Round(centre.Longitude, 2)));
        Assert.Empty(summaries[nothing.Id].Points!);
    }

    [Fact]
    public async Task Plan_of_a_missing_entry_returns_404()
    {
        var response = await _client.PutAsJsonAsync($"api/events/{Guid.NewGuid()}/plan", new VacationPlanDto());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Simultaneous_saves_do_not_fail()
    {
        var ev = await CreateVacationAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto { Destination = $"Destination {i}" })));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await _factory.WithDbAsync(db => db.VacationPlans.IgnoreQueryFilters().CountAsync()));
    }

    [Fact]
    public async Task Deleting_the_entry_deletes_the_plan()
    {
        var ev = await CreateVacationAsync();
        await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto { Destination = "Algarve" });

        (await _client.DeleteAsync($"api/events/{ev.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(0, await _factory.WithDbAsync(db => db.VacationPlans.IgnoreQueryFilters().CountAsync()));
    }

    [Fact]
    public async Task Vacation_summary_counts_working_days_and_spending()
    {
        // 3 to 14 August 2026: 10 weekdays, no holidays
        var ev = await CreateVacationAsync();
        await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto
        {
            Bookings = [new PlanBookingDto { Type = BookingType.Lodging, Name = "Hotel", Cost = 500 }],
            Expenses = [new PlanExpenseDto { Category = ExpenseCategory.Food, Planned = 200, Actual = 250 }],
            Tasks = [new PlanTaskDto { Text = "Suitcases", Done = true }, new PlanTaskDto { Text = "Tickets" }],
        });

        var summary = Assert.Single((await _client.GetFromJsonAsync<List<VacationSummaryDto>>("api/vacations"))!);

        Assert.Equal(10, summary.Workdays);
        Assert.Equal(700m, summary.Planned);
        Assert.Equal(750m, summary.Spent);
        Assert.Equal((2, 1), (summary.Tasks, summary.TasksDone));
    }

    [Fact]
    public async Task Lodgings_keep_the_stay_and_do_not_count_as_places_to_visit()
    {
        var ev = await CreateVacationAsync();
        await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto
        {
            Places =
            [
                new PlanPlaceDto { Name = "Hotel Faro", IsLodging = true, Date = new DateOnly(2026, 8, 3), EndDate = new DateOnly(2026, 8, 8), Visited = true },
                new PlanPlaceDto { Name = "Lagos house", IsLodging = true, Date = new DateOnly(2026, 8, 8) }, // no end: a single day
                new PlanPlaceDto { Name = "Beach", Date = new DateOnly(2026, 8, 4), EndDate = new DateOnly(2026, 8, 6), Visited = true },
                new PlanPlaceDto { Name = "Lighthouse" },
            ],
        });

        var plan = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");
        var summary = Assert.Single((await _client.GetFromJsonAsync<List<VacationSummaryDto>>("api/vacations"))!);

        var (faro, lagos, beach) = (plan!.Places[0], plan.Places[1], plan.Places[2]);
        Assert.Equal((new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 8)), (faro.Date, faro.EndDate));
        Assert.False(faro.Visited);
        Assert.Equal(new DateOnly(2026, 8, 8), lagos.EndDate);
        Assert.Null(beach.EndDate); // only lodging has an end
        Assert.True(faro.StaysOn(new DateOnly(2026, 8, 8)) && lagos.StaysOn(new DateOnly(2026, 8, 8))); // moving day
        Assert.Equal((2, 1), (summary.Places, summary.PlacesVisited));
    }

    [Fact]
    public async Task Lodging_with_check_out_before_check_in_or_outside_the_vacation_is_rejected()
    {
        var ev = await CreateVacationAsync();

        var backwards = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto
        {
            Places = [new PlanPlaceDto { Name = "Hotel", IsLodging = true, Date = new DateOnly(2026, 8, 6), EndDate = new DateOnly(2026, 8, 4) }],
        });
        var outside = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto
        {
            Places = [new PlanPlaceDto { Name = "Hotel", IsLodging = true, Date = new DateOnly(2026, 8, 6), EndDate = new DateOnly(2026, 8, 20) }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
    }

    [Fact]
    public async Task Holiday_does_not_count_as_a_working_day()
    {
        // 11 to 15 August 2025: Monday to Friday, but 15/08 (Assumption) is a holiday → 4 working days
        await CreateVacationAsync("2025-08-11", "2025-08-15");

        var summary = Assert.Single((await _client.GetFromJsonAsync<List<VacationSummaryDto>>("api/vacations"))!);

        Assert.Equal(4, summary.Workdays);
    }

    [Fact]
    public async Task Simultaneous_settings_saves_do_not_fail()

    {
        var settings = new UserSettingsDto { AnnualVacationDays = 25 };

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _client.PutAsJsonAsync("api/settings", settings)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(25, (await _client.GetFromJsonAsync<UserSettingsDto>("api/settings"))!.AnnualVacationDays);
    }
}
