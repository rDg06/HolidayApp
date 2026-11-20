using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>"Vacations" page: each trip's numbers.</summary>
public sealed class VacationsTests : BunitContext
{
    public VacationsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICalendarApi>(new FakeApi(Guid.NewGuid()));
    }

    [Fact]
    public void Card_shows_the_total_days_and_the_places()
    {
        var cut = Render<Vacations>();
        cut.WaitForElement(".vacation-stats");

        var stats = cut.FindAll(".vacation-stats > div")
            .ToDictionary(d => d.QuerySelector("dt")!.TextContent.Trim(), d => d.QuerySelector("dd")!.TextContent.Trim());

        Assert.StartsWith("5", stats["Days"]);
        Assert.Contains("4 working", stats["Days"]);
        Assert.StartsWith("3", stats["Places"]);
        Assert.Contains("1 visited", stats["Places"]);
    }
}
