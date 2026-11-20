using HolidayApp.Client.Components;

namespace HolidayApp.Client.Tests;

/// <summary>"Other places I've been": search, choose the year and add.</summary>
public sealed class VisitedPlacesTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    public VisitedPlacesTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./js/plan-map.js");
        _module.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Searches_chooses_the_year_and_adds()
    {
        _module.Setup<List<VisitedPlaces.SearchResult>?>("search", _ => true).SetResult(
        [
            new VisitedPlaces.SearchResult { Name = "Paris", Description = "Paris, France", Lat = 48.85, Lng = 2.35, Country = "France" },
            new VisitedPlaces.SearchResult { Name = "Paris", Description = "Paris, Texas, USA", Lat = 33.66, Lng = -95.55, Country = "United States" },
        ]);
        SaveVisitedPlaceRequest? added = null;
        var cut = Render<VisitedPlaces>(p => p
            .Add(x => x.Places, [])
            .Add(x => x.CurrentYear, 2026)
            .Add(x => x.OnAdd, r => { added = r; return Task.CompletedTask; }));

        cut.Find("input[aria-label='Search for a place']").Input("Paris");
        cut.Find(".visited-form .btn").Click();
        cut.FindAll(".visited-result")[0].Click();
        cut.Find("input[aria-label='Year I was there']").Change(2019);
        cut.Find(".btn.primary").Click();

        Assert.Equal(new SaveVisitedPlaceRequest("Paris", 48.85, 2.35, "France", 2019), added);
        Assert.Empty(cut.FindAll(".visited-result")); // clears the search
    }

    [Fact]
    public void A_future_year_is_not_added()
    {
        _module.Setup<List<VisitedPlaces.SearchResult>?>("search", _ => true).SetResult(
            [new VisitedPlaces.SearchResult { Name = "Moon", Lat = 1, Lng = 1 }]);
        var called = false;
        var cut = Render<VisitedPlaces>(p => p
            .Add(x => x.Places, [])
            .Add(x => x.CurrentYear, 2026)
            .Add(x => x.OnAdd, _ => { called = true; return Task.CompletedTask; }));

        cut.Find("input[aria-label='Search for a place']").Input("Moon");
        cut.Find(".visited-form .btn").Click(); // a single result: it is chosen
        cut.Find("input[aria-label='Year I was there']").Change(2030);
        cut.Find(".btn.primary").Click();

        Assert.False(called);
        Assert.Contains("between 1900 and 2026", cut.Find(".visited-error").TextContent);
    }

    [Fact]
    public void Lists_by_year_most_recent_first_and_deletes()
    {
        VisitedPlaceDto[] places =
        [
            new(Guid.NewGuid(), "Paris", 48.85, 2.35, "France", 2019),
            new(Guid.NewGuid(), "Cusco", -13.5, -71.9, "Peru", 2023),
            new(Guid.NewGuid(), "Berlin", 52.5, 13.4, "Germany", 2019),
        ];
        VisitedPlaceDto? deleted = null;
        var cut = Render<VisitedPlaces>(p => p
            .Add(x => x.Places, places)
            .Add(x => x.OnDelete, d => deleted = d));

        Assert.Equal(["2023", "2019"], cut.FindAll(".visited-years h3").Select(h => h.TextContent));
        Assert.Equal(["Cusco", "Berlin", "Paris"], cut.FindAll(".visited-name").Select(n => n.TextContent));

        cut.Find("button[aria-label='Delete Paris (2019)']").Click();
        Assert.Same(places[0], deleted);
    }
}
