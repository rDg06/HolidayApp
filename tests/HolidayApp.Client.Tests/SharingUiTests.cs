using HolidayApp.Client.Components;
using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>Who sees a trip: the sharing panel, view-only trips and the vacation cards.</summary>
public sealed class SharingUiTests : BunitContext
{
    private static readonly Guid TripId = Guid.NewGuid();
    private readonly FakeApi _api = new(TripId) { Me = new AccountDto("Ana", "ana@example.com", "ana") };

    public SharingUiTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/plan-map.js").Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICalendarApi>(_api);
    }

    private IRenderedComponent<TripSharing> RenderSharing() =>
        Render<TripSharing>(p => p
            .Add(x => x.EventId, TripId)
            .Add(x => x.Sharing, _api.Sharing)
            .Add(x => x.MyUsername, _api.Me!.Username));

    [Fact]
    public void Owner_adds_a_friend_and_makes_the_trip_public()
    {
        var cut = RenderSharing();
        Assert.Equal("true", cut.FindAll("[role=radio]").Single(b => b.TextContent.Trim() == "Private").GetAttribute("aria-checked"));

        cut.Find(".add-person input").Input("rui@example.com");
        cut.Find(".add-person select").Change("view");
        cut.Find(".add-person").Submit();
        cut.FindAll("[role=radio]").Single(b => b.TextContent.Trim() == "Public").Click();

        var member = Assert.Single(_api.Sharing.Members);
        Assert.Equal(("rui", false), (member.Username, member.CanEdit));
        Assert.True(_api.Sharing.IsPublic);
        Assert.Contains("@rui", cut.Find(".people").TextContent);
        Assert.Contains("feed", cut.Find(".sharing-hint").TextContent);
        Assert.Equal("", cut.Find(".add-person input").GetAttribute("value"));
    }

    [Fact]
    public void Adding_someone_without_an_account_shows_the_error()
    {
        var cut = RenderSharing();

        cut.Find(".add-person input").Input("nobody@example.com");
        cut.Find(".add-person").Submit();

        Assert.Contains("no account", cut.Find(".form-errors").TextContent);
        Assert.Empty(_api.Sharing.Members);
    }

    [Fact]
    public void Owner_changes_what_a_friend_can_do_and_removes_them()
    {
        _api.Sharing = _api.Sharing with { Members = [new TripMemberDto("rui", "Rui", false)] };
        var cut = RenderSharing();

        cut.Find(".people select").Change("edit");
        Assert.True(Assert.Single(_api.Sharing.Members).CanEdit);

        cut.Find("button[aria-label='Remove Rui']").Click();
        Assert.Empty(_api.Sharing.Members);
    }

    [Fact]
    public void Someone_added_sees_the_people_and_can_leave_but_not_change_anything()
    {
        _api.Sharing = new TripSharingDto(TripRole.Editor, false, new PersonDto("rui", "Rui"), [new TripMemberDto("ana", "Ana", true)]);
        var left = false;
        var cut = Render<TripSharing>(p => p
            .Add(x => x.EventId, TripId)
            .Add(x => x.Sharing, _api.Sharing)
            .Add(x => x.MyUsername, "ana")
            .Add(x => x.OnLeft, () => left = true));

        Assert.Empty(cut.FindAll(".add-person"));
        Assert.Empty(cut.FindAll("[role=radio]"));
        Assert.Contains("Ana (you)", cut.Find(".people").TextContent);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Leave")).Click();

        Assert.True(left);
        Assert.Empty(_api.Sharing.Members);
    }

    [Fact]
    public void A_view_only_trip_opens_the_read_only_summary()
    {
        _api.Sharing = new TripSharingDto(TripRole.Viewer, false, new PersonDto("rui", "Rui"), [new TripMemberDto("ana", "Ana", false)]);
        var navigation = Services.GetRequiredService<NavigationManager>();

        Render<VacationPlanner>(p => p.Add(x => x.Id, TripId));

        Assert.EndsWith($"vacations/{TripId}/share?from=vacations", navigation.Uri);
    }

    [Fact]
    public void Read_only_summary_says_whose_trip_it_is_and_goes_back_to_the_list()
    {
        _api.Sharing = new TripSharingDto(TripRole.PublicViewer, true, new PersonDto("rui", "Rui"), []);

        var cut = Render<VacationShare>(p => p.Add(x => x.Id, TripId));
        cut.WaitForElement(".share-owner");

        Assert.Contains("Public trip", cut.Find(".share-owner").TextContent);
        Assert.Contains("Rui", cut.Find(".share-owner").TextContent);
        Assert.Equal("vacations", cut.Find(".back-link").GetAttribute("href"));
    }

    [Fact]
    public void Vacation_cards_show_private_people_and_trips_shared_with_me()
    {
        _api.MoreVacations.Add(new VacationSummaryDto(Guid.NewGuid(), "Porto", new(2026, 12, 1), new(2026, 12, 3), 3, null, 0, 0, 0, 0, 0, 0, 0,
            Role: TripRole.Viewer, Owner: new PersonDto("rui", "Rui")));

        var cut = Render<Vacations>();
        cut.WaitForElement(".vacation-sharing");

        var cards = cut.FindAll(".vacation-card");
        var porto = cards.Single(c => c.TextContent.Contains("Porto"));
        Assert.Contains("Private", cards.Single(c => c.TextContent.Contains("Roma")).TextContent);
        Assert.Contains("Rui's trip", porto.TextContent);
        Assert.Contains("view only", porto.TextContent);
        Assert.EndsWith("/share?from=vacations", porto.GetAttribute("href"));
        Assert.Contains("1 shared with you", cut.Find(".vacations-header").TextContent);
    }
}
