using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>Share/PDF summary: by default without booking amounts or references.</summary>
public sealed class VacationShareTests : BunitContext
{
    private static readonly Guid TripId = Guid.NewGuid();
    private static DateOnly Nov(int day) => new(2026, 11, day);

    public VacationShareTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/plan-map.js").Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICalendarApi>(new FakeApi(TripId));
    }

    private IRenderedComponent<VacationShare> RenderShare()
    {
        var cut = Render<VacationShare>(p => p.Add(x => x.Id, TripId));
        cut.WaitForElement(".share-doc");
        return cut;
    }

    [Fact]
    public void Shows_day_by_day_with_the_route_and_the_other_places()
    {
        var cut = RenderShare();

        Assert.Equal(3, cut.FindAll(".share-day").Count);
        var day3 = cut.FindAll(".share-day")[1].TextContent;
        Assert.Contains("Hotel Roma", day3);
        Assert.Contains("Colosseum", day3);
        Assert.Contains("Free day", cut.FindAll(".share-day")[2].TextContent);
        Assert.Contains("Metro Colosseo", cut.Find(".share-section:has(h2:contains('Other places'))").TextContent);
    }

    [Fact]
    public void Amounts_and_references_only_show_if_chosen()
    {
        var cut = RenderShare();
        var text = cut.Find(".share-doc").TextContent;
        Assert.DoesNotContain("TP832", text);
        Assert.DoesNotContain("189.90", text);
        Assert.Empty(cut.FindAll("h2:contains('Spending')"));

        cut.Find("input[type=checkbox]:not(:checked)").Change(true); // Amounts
        cut.FindAll("input[type=checkbox]")[^1].Change(true);     // References and links

        text = cut.Find(".share-doc").TextContent;
        Assert.Contains("TP832", text);
        Assert.Contains("€189.90", text);
        Assert.NotEmpty(cut.FindAll("h2:contains('Spending')"));
    }

    [Fact]
    public void Save_PDF_opens_the_browsers_print_dialog()
    {
        var cut = RenderShare();

        cut.Find("button.primary").Click();

        JSInterop.VerifyInvoke("print");
    }
}
