using HolidayApp.Client.Components;
using HolidayApp.Client.Services;

namespace HolidayApp.Client.Tests;

/// <summary>Places map: day filter, lodgings, order and route (the map itself is JavaScript and is left out).</summary>
public sealed class PlaceMapTests : BunitContext
{
    private static readonly DateOnly TripStart = new(2026, 11, 2);
    private static readonly DateOnly TripEnd = new(2026, 11, 6);
    private static DateOnly Nov(int day) => new(2026, 11, day);

    public PlaceMapTests()
    {
        // Leaflet does not run in tests: JavaScript calls return empty values
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./js/plan-map.js");
        _module.Mode = JSRuntimeMode.Loose;
        Routing.ClearCache();
    }

    private readonly BunitJSModuleInterop _module;

    private IRenderedComponent<PlaceMap> RenderMap(List<PlanPlaceDto> places, DateOnly? today = null) =>
        Render<PlaceMap>(p => p
            .Add(x => x.Places, places)
            .Add(x => x.TripStart, TripStart)
            .Add(x => x.TripEnd, TripEnd)
            .Add(x => x.Today, today ?? new DateOnly(2026, 1, 1)));

    private static List<string> Names(IRenderedComponent<PlaceMap> cut) =>
        cut.FindAll("input[aria-label='Place name']").Select(i => i.GetAttribute("value") ?? "").ToList();

    private static void ClickChip(IRenderedComponent<PlaceMap> cut, string text) =>
        cut.FindAll(".chip").First(c => c.TextContent.Contains(text)).Click();

    /// <summary>Opens a place's kind menu and picks an option.</summary>
    private static void ChooseKind(IRenderedComponent<PlaceMap> cut, string place, string option)
    {
        cut.Find($"button[aria-label^='Type of {place}:']").Click();
        cut.FindAll(".kind-option").First(o => o.TextContent.Contains(option)).Click();
    }

    private static List<PlanPlaceDto> Trip() =>
    [
        new() { Name = "Hotel Roma", IsLodging = true, Date = Nov(2), EndDate = Nov(4), Latitude = 41.9, Longitude = 12.49 },
        new() { Name = "Colosseum", Date = Nov(3), Latitude = 41.8902, Longitude = 12.4922 },
        new() { Name = "Pantheon", Date = Nov(3), Latitude = 41.8986, Longitude = 12.4769 },
        new() { Name = "Hotel Paris", IsLodging = true, Date = Nov(4), EndDate = Nov(6), Latitude = 48.86, Longitude = 2.35 },
        new() { Name = "Eiffel Tower", Date = Nov(5), Latitude = 48.8584, Longitude = 2.2945 },
        new() { Name = "Louvre" },
    ];

    [Fact]
    public void All_shows_everything()
    {
        var cut = RenderMap(Trip());

        Assert.Equal(["Hotel Roma", "Colosseum", "Pantheon", "Hotel Paris", "Eiffel Tower", "Louvre"], Names(cut));
    }

    [Fact]
    public void A_day_shows_that_days_places_and_that_nights_lodging()
    {
        var cut = RenderMap(Trip());

        ClickChip(cut, "Tue, 3 Nov");
        Assert.Equal(["Hotel Roma", "Colosseum", "Pantheon"], Names(cut));

        // Moving day: both lodgings
        ClickChip(cut, "Wed, 4 Nov");
        Assert.Equal(["Hotel Roma", "Hotel Paris"], Names(cut));

        ClickChip(cut, "No day");
        Assert.Equal(["Louvre"], Names(cut));
    }

    [Fact]
    public void During_the_trip_it_opens_on_today()
    {
        var cut = RenderMap(Trip(), today: Nov(5));

        Assert.Equal(["Hotel Paris", "Eiffel Tower"], Names(cut));
        var today = cut.Find(".chip[aria-pressed='true']");
        Assert.Contains("Today", today.TextContent);
    }

    [Fact]
    public void Days_route_leaves_the_lodging_and_shows_the_distances()
    {
        var cut = RenderMap(Trip());

        ClickChip(cut, "Tue, 3 Nov");

        Assert.Contains("Day's route", cut.Find(".route-total").TextContent);
        var legs = cut.FindAll(".leg").Select(l => l.TextContent).ToList();
        Assert.Equal(2, legs.Count);
        Assert.Contains("from Hotel Roma", legs[0]);
        Assert.Contains("from Colosseum", legs[1]);
    }

    [Fact]
    public async Task Dragging_changes_the_order_and_the_numbers()
    {
        var places = Trip();
        var cut = RenderMap(places);
        ClickChip(cut, "Tue, 3 Nov");

        // Colosseum (position 1) dropped after the Pantheon (position 2)
        await cut.InvokeAsync(() => cut.Instance.OnReorder(1, 2, after: true));

        Assert.Equal(["Hotel Roma", "Pantheon", "Colosseum"], Names(cut));
        Assert.Equal("Pantheon", places[1].Name);
        Assert.Equal("1", cut.FindAll(".place-item")[1].QuerySelector(".place-number")!.TextContent.Trim());
    }

    [Fact]
    public async Task Dragging_before_a_place_elsewhere_in_the_list()
    {
        var places = Trip();
        var cut = RenderMap(places);

        // Louvre (last) dropped before the Colosseum
        await cut.InvokeAsync(() => cut.Instance.OnReorder(5, 1, after: false));

        Assert.Equal(["Hotel Roma", "Louvre", "Colosseum", "Pantheon", "Hotel Paris", "Eiffel Tower"], places.Select(p => p.Name));
    }

    [Fact]
    public async Task Arrow_keys_on_the_handle_swap_with_the_visible_neighbour()
    {
        var places = Trip();
        var cut = RenderMap(places);
        ClickChip(cut, "Tue, 3 Nov");

        var moved = await cut.InvokeAsync(() => cut.Instance.OnReorderStep(1, +1));
        var stuck = await cut.InvokeAsync(() => cut.Instance.OnReorderStep(moved, +1)); // already the last one of the day

        Assert.Equal(2, moved);
        Assert.Equal(2, stuck);
        Assert.Equal(["Hotel Roma", "Pantheon", "Colosseum"], Names(cut));
    }

    [Fact]
    public void Only_places_to_visit_have_a_drag_handle()
    {
        var cut = RenderMap(Trip());

        Assert.Equal(4, cut.FindAll(".drag-handle").Count);
        Assert.Empty(cut.FindAll("button[aria-label^='Move up']"));
    }

    [Fact]
    public void Turning_into_lodging_lasts_from_the_places_day_to_the_end_of_the_trip()
    {
        var places = Trip();
        var cut = RenderMap(places);

        ChooseKind(cut, "Colosseum", "Lodging");

        var colosseum = places[1];
        Assert.True(colosseum.IsLodging);
        Assert.Equal((Nov(3), TripEnd), (colosseum.Date, colosseum.EndDate));
    }

    [Fact]
    public void First_day_after_the_last_pushes_the_last()
    {
        var places = Trip();
        var cut = RenderMap(places);

        cut.FindAll("select[aria-label='First day at the lodging']")[0].Change("2026-11-05");

        Assert.Equal((Nov(5), Nov(5)), (places[0].Date, places[0].EndDate));
    }

    [Fact]
    public async Task Clicking_the_map_with_a_day_chosen_adds_the_place_on_that_day()
    {
        var places = Trip();
        var cut = RenderMap(places);
        ClickChip(cut, "Fri, 6 Nov");

        await cut.InvokeAsync(() => cut.Instance.OnMapClick(48.85, 2.34));

        Assert.Equal(Nov(6), places[^1].Date);
        Assert.Equal("Place 5", places[^1].Name); // lodgings do not count for the number
    }

    [Fact]
    public void Notes_and_link_open_with_the_pencil()
    {
        var places = Trip();
        var cut = RenderMap(places);

        cut.Find("button[aria-label='Notes and link for Louvre']").Click();
        cut.Find("textarea[aria-label='Notes for Louvre']").Change("Closed on Tuesdays");
        cut.Find("input[aria-label='Link for Louvre']").Change("https://www.louvre.fr");

        Assert.Equal("Closed on Tuesdays", places[5].Notes);
        Assert.Equal("https://www.louvre.fr", cut.Find("a[href='https://www.louvre.fr']").GetAttribute("href"));
    }

    [Fact]
    public void Metro_without_a_day_shows_on_every_day_but_outside_the_route()
    {
        var places = Trip();
        places.Add(new() { Name = "Metro Colosseo", Kind = PlaceKind.Metro, Latitude = 41.891, Longitude = 12.4923 });
        var cut = RenderMap(places);

        ClickChip(cut, "Tue, 3 Nov");

        Assert.Contains("Metro Colosseo", Names(cut));
        Assert.DoesNotContain(cut.FindAll(".leg"), l => l.TextContent.Contains("Metro Colosseo"));
        Assert.Equal(PlaceKind.Metro, places[^1].Kind);
    }

    [Fact]
    public void Airport_with_a_day_joins_that_days_route()
    {
        var places = Trip();
        places.Insert(1, new() { Name = "Fiumicino", Kind = PlaceKind.Airport, Date = Nov(3), Latitude = 41.8, Longitude = 12.25 });
        var cut = RenderMap(places);

        ClickChip(cut, "Tue, 3 Nov");

        var legs = cut.FindAll(".leg").Select(l => l.TextContent).ToList();
        Assert.Contains(legs, l => l.Contains("from Hotel Roma")); // airport right after the hotel
        Assert.Contains(legs, l => l.Contains("from Fiumicino"));
    }

    [Fact]
    public void Turning_into_a_bus_stop_removes_the_number_and_visited()
    {
        var places = Trip();
        places[2].Visited = true; // Pantheon
        var cut = RenderMap(places);

        ChooseKind(cut, "Pantheon", "Bus stop");

        var item = cut.FindAll(".place-item")[2];
        Assert.Equal(PlaceKind.Bus, places[2].Kind);
        Assert.False(places[2].Visited);
        Assert.Contains("kind-bus", item.ClassName);
        Assert.Null(item.QuerySelector("input[type=checkbox]"));
        // Eiffel Tower goes from 3 to 2: transport has no number
        Assert.Equal("2", cut.FindAll(".place-item")[4].QuerySelector(".place-number")!.TextContent.Trim());
    }

    [Fact]
    public void No_longer_lodging_removes_the_last_day()
    {
        var places = Trip();
        var cut = RenderMap(places);

        ChooseKind(cut, "Hotel Roma", "Train station");

        Assert.Equal(PlaceKind.Train, places[0].Kind);
        Assert.Equal(Nov(2), places[0].Date);
        Assert.Null(places[0].EndDate);
    }

    [Fact]
    public void Only_one_kind_menu_open_at_a_time()
    {
        var cut = RenderMap(Trip());

        cut.Find("button[aria-label^='Type of Colosseum:']").Click();
        cut.Find("button[aria-label^='Type of Pantheon:']").Click();

        var menu = Assert.Single(cut.FindAll(".kind-menu"));
        Assert.Equal("Type of Pantheon", menu.GetAttribute("aria-label"));

        // Clicking the same button again closes the menu
        cut.Find("button[aria-label^='Type of Pantheon:']").Click();
        Assert.Empty(cut.FindAll(".kind-menu"));
    }

    // ---------- Street route ----------

    /// <summary>Fake route: Hotel → Colosseum (1.2 km, 15 min) → Pantheon (2.1 km, 28 min).</summary>
    private static RouteResult FakeRoute() => new()
    {
        Distance = 3300,
        Duration = 2580,
        Legs = [new RouteLeg { Distance = 1200, Duration = 900 }, new RouteLeg { Distance = 2100, Duration = 1680 }],
        Geometry = [[41.9, 12.49], [41.8902, 12.4922], [41.8986, 12.4769]],
    };

    [Fact]
    public void A_day_shows_the_street_route_with_times()
    {
        _module.Setup<RouteResult?>("route", _ => true).SetResult(FakeRoute());
        var cut = RenderMap(Trip());

        ClickChip(cut, "Tue, 3 Nov");

        cut.WaitForAssertion(() => Assert.Contains("3.3 km · 43 min on foot", cut.Find(".route-total").TextContent));
        var legs = cut.FindAll(".leg").Select(l => l.TextContent).ToList();
        Assert.Contains("1.2 km · 15 min from Hotel Roma", legs[0]);
        Assert.Contains("2.1 km · 28 min from Colosseum", legs[1]);
    }

    [Fact]
    public void Without_the_routing_service_the_straight_line_stays()
    {
        _module.Setup<RouteResult?>("route", _ => true).SetException(new Microsoft.JSInterop.JSException("no internet"));
        var cut = RenderMap(Trip());

        ClickChip(cut, "Tue, 3 Nov");

        cut.WaitForAssertion(() => Assert.Contains("no street route", cut.Find(".route-total").TextContent));
        Assert.Contains("straight-line from Hotel Roma", cut.FindAll(".leg")[0].TextContent);
    }

    [Fact]
    public void Switching_to_car_keeps_the_mode_and_asks_for_the_car_route()
    {
        var route = _module.Setup<RouteResult?>("route", _ => true);
        route.SetResult(FakeRoute());
        RouteMode? chosen = null;
        var cut = Render<PlaceMap>(p => p
            .Add(x => x.Places, Trip())
            .Add(x => x.TripStart, TripStart)
            .Add(x => x.TripEnd, TripEnd)
            .Add(x => x.Today, new DateOnly(2026, 1, 1))
            .Add(x => x.RouteModeChanged, m => chosen = m));
        ClickChip(cut, "Tue, 3 Nov");

        cut.FindAll(".route-modes .chip").First(c => c.TextContent == "Car").Click();

        Assert.Equal(RouteMode.Car, chosen);
        cut.WaitForAssertion(() => Assert.Contains(route.Invocations, i => (string?)i.Arguments[1] == "Car"));
        Assert.Contains("by car", cut.Find(".route-total").TextContent);

    }
}
