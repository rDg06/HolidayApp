using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>Planner: tabs below the dates.</summary>
public sealed class VacationPlannerTests : BunitContext
{
    private static readonly Guid TripId = Guid.NewGuid();

    public VacationPlannerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/plan-map.js").Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICalendarApi>(new FakeApi(TripId));
    }

    private IRenderedComponent<VacationPlanner> RenderPlanner()
    {
        var cut = Render<VacationPlanner>(p => p.Add(x => x.Id, TripId));
        cut.WaitForElement(".plan-tabs");
        return cut;
    }

    private static void ClickTab(IRenderedComponent<VacationPlanner> cut, string label) =>
        cut.FindAll("[role=tab]").First(t => t.TextContent.Contains(label)).Click();

    private static string SelectedTab(IRenderedComponent<VacationPlanner> cut) =>
        cut.Find("[role=tab][aria-selected=true]").TextContent;

    [Fact]
    public void Opens_on_the_Places_tab_with_the_destination_and_the_map()
    {
        var cut = RenderPlanner();

        Assert.Contains("Places", SelectedTab(cut));
        Assert.NotEmpty(cut.FindAll(".place-map"));
        Assert.Equal("Roma", cut.Find("input[placeholder^='e.g. Algarve']").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".booking-list"));
    }

    [Fact]
    public void Tabs_show_the_numbers()
    {
        var cut = RenderPlanner();

        var tabs = cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()).ToList();
        Assert.Equal(5, tabs.Count);
        Assert.Contains(tabs, t => t.StartsWith("Places") && t.EndsWith('1'));      // 1 visit (hotel and metro do not count)
        Assert.Contains(tabs, t => t.StartsWith("Bookings") && t.EndsWith('2'));
        Assert.Contains(tabs, t => t.StartsWith("Tasks") && t.EndsWith("1/2"));
    }

    [Fact]
    public void Each_tab_only_shows_its_part()
    {
        var cut = RenderPlanner();

        ClickTab(cut, "Bookings");
        Assert.NotEmpty(cut.FindAll(".booking-list"));
        Assert.Empty(cut.FindAll(".place-map"));

        ClickTab(cut, "Budget");
        Assert.Contains("Budget", cut.Find(".plan-grid").TextContent);
        Assert.Contains("Where I spent most", cut.Find(".plan-grid").TextContent);

        ClickTab(cut, "Day by day");
        Assert.Contains("Day by day", cut.Find(".plan-grid").TextContent);
    }

    [Fact]
    public void Arrow_keys_change_tab()
    {
        var cut = RenderPlanner();

        cut.Find(".plan-tabs").KeyDown(Key.Right);
        Assert.Contains("Bookings", SelectedTab(cut));

        cut.Find(".plan-tabs").KeyDown(Key.Left);
        cut.Find(".plan-tabs").KeyDown(Key.Left);
        Assert.Contains("Day by day", SelectedTab(cut)); // wraps around
    }

    [Fact]
    public void Put_on_map_on_a_booking_goes_to_the_Places_tab()
    {
        var cut = RenderPlanner();
        ClickTab(cut, "Bookings");

        cut.Find(".put-on-map").Click();

        Assert.Contains("Places", SelectedTab(cut));
        cut.WaitForAssertion(() => Assert.Contains("Hotel Trevi", cut.Find(".lodging-search").TextContent));
    }

    [Theory]
    [InlineData(null, "calendar", "← Calendar")]
    [InlineData("vacations", "vacations", "← Vacations")]
    [InlineData("map", "map", "← Map")]
    [InlineData("feed", "", "← Feed")] // the feed is the home page
    public void Back_link_goes_where_you_came_from(string? from, string href, string label)
    {
        // "from" comes from the address (?from=…), as when the trip is opened from another page
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo(from is null ? $"vacations/{TripId}" : $"vacations/{TripId}?from={from}");
        var cut = Render<VacationPlanner>(p => p.Add(x => x.Id, TripId));
        cut.WaitForElement(".plan-tabs");

        var back = cut.Find(".back-link");
        Assert.Equal(href, back.GetAttribute("href"));
        Assert.Equal(label, back.TextContent);
    }
}
