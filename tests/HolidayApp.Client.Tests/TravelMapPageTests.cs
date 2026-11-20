using HolidayApp.Client.Components;
using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>Map page: adding and deleting places updates the list and the map.</summary>
public sealed class TravelMapPageTests : BunitContext
{
    private readonly FakeApi _api = new(Guid.NewGuid());

    public TravelMapPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./js/plan-map.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<List<VisitedPlaces.SearchResult>?>("search", i => (string?)i.Arguments[0] == "Cusco")
            .SetResult([new VisitedPlaces.SearchResult { Name = "Cusco", Lat = -13.53, Lng = -71.97, Country = "Peru" }]);
        Services.AddSingleton<ICalendarApi>(_api);
    }

    [Fact]
    public void An_added_place_shows_in_the_list_and_the_countries()
    {
        var cut = Render<TravelMapPage>();
        cut.WaitForElement(".visited");

        cut.Find("input[aria-label='Search for a place']").Input("Cusco");
        cut.Find(".visited-form .btn").Click();
        cut.Find("input[aria-label='Year I was there']").Change(2023);
        cut.Find(".visited .btn.primary").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Cusco"], cut.FindAll(".visited-name").Select(n => n.TextContent)));
        Assert.Contains("Peru", cut.FindAll(".travel-countries li").Select(l => l.TextContent));
        Assert.Single(_api.Visited);
    }

    [Fact]
    public void Deleting_a_place_removes_it_from_the_list()
    {
        _api.Visited.Add(new VisitedPlaceDto(Guid.NewGuid(), "Paris", 48.85, 2.35, "France", 2019));
        var cut = Render<TravelMapPage>();
        cut.WaitForElement(".visited-name");

        cut.Find("button[aria-label='Delete Paris (2019)']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".visited-name")));
        Assert.Empty(_api.Visited);
    }
}
