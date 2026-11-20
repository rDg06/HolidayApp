using HolidayApp.Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>"Where I've been": numbers, countries and opening the trip from the map.</summary>
public sealed class TravelMapTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 11, 15);
    private readonly BunitJSModuleInterop _module;

    public TravelMapTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./js/plan-map.js");
        _module.Mode = JSRuntimeMode.Loose;
    }

    private static VacationSummaryDto Trip(string title, DateOnly start, DateOnly end, string? destination = null, params MapPointDto[] points) =>
        new(Guid.NewGuid(), title, start, end, 0, destination, 0, 0, 0, 0, 0, 0, 0, points);

    private static readonly VacationSummaryDto Roma = Trip("Rome and Paris", new(2026, 8, 3), new(2026, 8, 14), null,
        new MapPointDto("Hotel Roma", 41.9, 12.49), new MapPointDto("Hotel Paris", 48.86, 2.35));
    private static readonly VacationSummaryDto Algarve = Trip("Summer", new(2025, 8, 4), new(2025, 8, 15), "Lagos, Algarve");
    private static readonly VacationSummaryDto Natal = Trip("Christmas", new(2026, 12, 24), new(2026, 12, 31), "Porto");

    private IRenderedComponent<TravelMap> RenderMap() =>
        Render<TravelMap>(p => p
            .Add(x => x.Vacations, [Roma, Algarve, Natal])
            .Add(x => x.Today, Today));

    private static Dictionary<string, string> Stats(IRenderedComponent<TravelMap> cut) =>
        cut.FindAll(".travel-stats > div").ToDictionary(d => d.QuerySelector("dt")!.TextContent, d => d.QuerySelector("dd")!.TextContent.Trim());

    [Fact]
    public void Counts_trips_taken_days_and_upcoming()
    {
        var cut = RenderMap();

        var stats = Stats(cut);
        Assert.Equal("2", stats["Trips taken"]);
        Assert.Equal("24", stats["Days travelling"]); // 12 + 12
        Assert.Equal("1", stats["Upcoming"]);
    }

    [Fact]
    public void Countries_come_from_the_points_and_the_destination_search()
    {
        _module.Setup<List<TravelMap.SearchResult>?>("search", i => (string?)i.Arguments[0] == "Lagos, Algarve")
            .SetResult([new TravelMap.SearchResult { Lat = 37.1, Lng = -8.67, Country = "Portugal" }]);
        _module.Setup<string?>("country", i => (double)i.Arguments[0]! > 45).SetResult("France");
        _module.Setup<string?>("country", i => (double)i.Arguments[0]! < 45).SetResult("Italy");

        var cut = RenderMap();

        cut.WaitForAssertion(() => Assert.Equal("3", Stats(cut)["Countries"]), TimeSpan.FromSeconds(10));
        Assert.Equal(["France", "Italy", "Portugal"], cut.FindAll(".travel-countries li").Select(l => l.TextContent));
    }

    [Fact]
    public void Countries_looked_up_before_come_from_the_browser_without_asking_again()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "holidayapp.geo.v2").SetResult("""
            {"c:41.90,12.49":{"Country":"Italy"},"c:48.86,2.35":{"Country":"France"},
             "q:lagos, algarve":{"Lat":37.1,"Lng":-8.67,"Country":"Portugal"},"q:porto":{"Lat":41.15,"Lng":-8.61,"Country":"Portugal"}}
            """);

        var cut = RenderMap();

        cut.WaitForAssertion(() => Assert.Equal("3", Stats(cut)["Countries"]));
        Assert.DoesNotContain(_module.Invocations, i => i.Identifier is "search" or "country");
    }

    [Fact]
    public async Task Clicking_a_point_opens_the_trip()
    {
        var cut = RenderMap();

        await cut.InvokeAsync(() => cut.Instance.OnPlaceClick(0));

        Assert.EndsWith($"/vacations/{Roma.Id}?from=map", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Places_added_by_hand_count_for_the_countries()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "holidayapp.geo.v2").SetResult("""
            {"c:41.90,12.49":{"Country":"Italy"},"c:48.86,2.35":{"Country":"France"},
             "q:lagos, algarve":{"Lat":37.1,"Lng":-8.67,"Country":"Portugal"},"q:porto":{"Lat":41.15,"Lng":-8.61,"Country":"Portugal"}}
            """);

        var cut = Render<TravelMap>(p => p
            .Add(x => x.Vacations, [Roma, Algarve, Natal])
            .Add(x => x.VisitedPlaces, [new VisitedPlaceDto(Guid.NewGuid(), "Cusco", -13.5, -71.9, "Peru", 2023)])
            .Add(x => x.Today, Today));

        cut.WaitForAssertion(() => Assert.Equal("4", Stats(cut)["Countries"]));
        Assert.Contains("Peru", cut.FindAll(".travel-countries li").Select(l => l.TextContent));
        Assert.Contains("Other places", cut.Find(".travel-legend").TextContent);

    }
}
