using System.Net;
using System.Net.Http.Json;
using HolidayApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Api.Tests;

/// <summary>The feed of public trips, likes and comments.</summary>
public sealed class FeedTests : IAsyncLifetime
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

    private static async Task<Guid> TripAsync(HttpClient client, string title, bool isPublic = true)
    {
        var response = await client.PostAsJsonAsync("api/events",
            new SaveCalendarEventRequest(title, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 7), EventType.Vacation, null));
        var id = (await response.Content.ReadFromJsonAsync<CalendarEventDto>())!.Id;
        if (isPublic)
            (await client.PutAsJsonAsync($"api/events/{id}/sharing", new SetTripVisibilityRequest(true))).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<FeedPageDto> FeedAsync(HttpClient client, int skip = 0) =>
        (await client.GetFromJsonAsync<FeedPageDto>($"api/feed?skip={skip}"))!;

    [Fact]
    public async Task Shows_public_trips_newest_first_with_their_map_without_private_ones()
    {
        var rome = await TripAsync(_ana, "Rome");
        await _ana.PutAsJsonAsync($"api/events/{rome}/plan", new VacationPlanDto
        {
            Places = [new PlanPlaceDto { Name = "Colosseum", Latitude = 41.89, Longitude = 12.49, Notes = "Book online" }],
        });
        await TripAsync(_rui, "Azores");
        await TripAsync(_eva, "Secret", isPublic: false);

        var feed = await FeedAsync(_eva);

        Assert.Equal(["Azores", "Rome"], feed.Trips.Select(t => t.Title));
        Assert.False(feed.HasMore);
        var place = Assert.Single(feed.Trips[1].Places);
        Assert.Equal(("Colosseum", 41.89), (place.Name, place.Latitude));
        Assert.Null(place.Notes); // the feed only sends what the map needs
    }

    [Fact]
    public async Task Comes_a_page_at_a_time()
    {
        for (var i = 1; i <= 12; i++)
            await TripAsync(_ana, $"Trip {i}");

        var first = await FeedAsync(_rui);
        var second = await FeedAsync(_rui, first.Trips.Count);

        Assert.Equal(10, first.Trips.Count);
        Assert.True(first.HasMore);
        Assert.Equal(["Trip 2", "Trip 1"], second.Trips.Select(t => t.Title));
        Assert.False(second.HasMore);
    }

    [Fact]
    public async Task Likes_count_once_per_person_and_can_be_undone()
    {
        var rome = await TripAsync(_ana, "Rome");

        await _rui.PutAsync($"api/events/{rome}/like", null);
        await _rui.PutAsync($"api/events/{rome}/like", null); // twice: still one
        var afterEva = await (await _eva.PutAsync($"api/events/{rome}/like", null)).Content.ReadFromJsonAsync<TripLikesDto>();
        Assert.Equal(new TripLikesDto(2, true), afterEva);

        var undone = await (await _rui.DeleteAsync($"api/events/{rome}/like")).Content.ReadFromJsonAsync<TripLikesDto>();
        Assert.Equal(new TripLikesDto(1, false), undone);

        var seenByEva = Assert.Single((await FeedAsync(_eva)).Trips);
        Assert.Equal((1, true), (seenByEva.Likes, seenByEva.LikedByMe));
        Assert.False(Assert.Single((await FeedAsync(_rui)).Trips).LikedByMe);
    }

    [Fact]
    public async Task Comments_show_on_the_feed_and_only_the_author_or_the_owner_delete_them()
    {
        var rome = await TripAsync(_ana, "Rome");
        TripCommentDto Post(HttpResponseMessage r) => r.Content.ReadFromJsonAsync<TripCommentDto>().Result!;

        var rui = Post(await _rui.PostAsJsonAsync($"api/events/{rome}/comments", new AddTripCommentRequest("  Amazing!  ")));
        var eva = Post(await _eva.PostAsJsonAsync($"api/events/{rome}/comments", new AddTripCommentRequest("Take me next time")));
        var ana = Post(await _ana.PostAsJsonAsync($"api/events/{rome}/comments", new AddTripCommentRequest("Thanks!")));
        Assert.Equal(("Amazing!", "rui", true), (rui.Text, rui.Author.Username, rui.CanDelete));

        // The feed has the count and the 2 newest, oldest first
        var trip = Assert.Single((await FeedAsync(_eva)).Trips);
        Assert.Equal(3, trip.CommentCount);
        Assert.Equal(["Take me next time", "Thanks!"], trip.LatestComments.Select(c => c.Text));

        // Eva cannot delete Rui's comment; Rui deletes his own; Ana (the owner) deletes Eva's
        Assert.Equal(HttpStatusCode.Forbidden, (await _eva.DeleteAsync($"api/events/{rome}/comments/{rui.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _rui.DeleteAsync($"api/events/{rome}/comments/{rui.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _ana.DeleteAsync($"api/events/{rome}/comments/{eva.Id}")).StatusCode);

        var left = (await _eva.GetFromJsonAsync<List<TripCommentDto>>($"api/events/{rome}/comments"))!;
        Assert.Equal([(ana.Id, false)], left.Select(c => (c.Id, c.CanDelete)));
    }

    [Fact]
    public async Task Empty_or_too_long_comments_are_refused()
    {
        var rome = await TripAsync(_ana, "Rome");

        var empty = await _rui.PostAsJsonAsync($"api/events/{rome}/comments", new AddTripCommentRequest("   "));
        var tooLong = await _rui.PostAsJsonAsync($"api/events/{rome}/comments", new AddTripCommentRequest(new string('a', 501)));

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (empty.StatusCode, tooLong.StatusCode));
    }

    [Fact]
    public async Task Private_trips_cannot_be_liked_or_commented_by_strangers_but_friends_can()
    {
        var secret = await TripAsync(_ana, "Secret", isPublic: false);
        await _ana.PostAsJsonAsync($"api/events/{secret}/sharing/members", new AddTripMemberRequest("rui", false));

        Assert.Equal(HttpStatusCode.NotFound, (await _eva.PutAsync($"api/events/{secret}/like", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _eva.PostAsJsonAsync($"api/events/{secret}/comments", new AddTripCommentRequest("Hi"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _eva.GetAsync($"api/events/{secret}/comments")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _rui.PutAsync($"api/events/{secret}/like", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _rui.PostAsJsonAsync($"api/events/{secret}/comments", new AddTripCommentRequest("See you there"))).StatusCode);
    }

    [Fact]
    public async Task Making_a_trip_public_again_puts_it_back_on_top()
    {
        var rome = await TripAsync(_ana, "Rome");
        await TripAsync(_rui, "Azores");
        await _ana.PutAsJsonAsync($"api/events/{rome}/sharing", new SetTripVisibilityRequest(false));
        await _ana.PutAsJsonAsync($"api/events/{rome}/sharing", new SetTripVisibilityRequest(true));

        Assert.Equal(["Rome", "Azores"], (await FeedAsync(_eva)).Trips.Select(t => t.Title));
    }

    [Fact]
    public async Task Deleting_an_account_removes_its_likes_and_comments_and_those_on_its_trips()
    {
        var rome = await TripAsync(_ana, "Rome");
        var azores = await TripAsync(_rui, "Azores");
        await _rui.PutAsync($"api/events/{rome}/like", null);
        await _rui.PostAsJsonAsync($"api/events/{rome}/comments", new AddTripCommentRequest("Nice"));
        await _ana.PutAsync($"api/events/{azores}/like", null);
        await _ana.PostAsJsonAsync($"api/events/{azores}/comments", new AddTripCommentRequest("Lovely"));

        (await _ana.DeleteAsync("api/me")).EnsureSuccessStatusCode();

        Assert.Equal(0, await _factory.WithDbAsync(db => db.TripLikes.CountAsync()));
        Assert.Equal(0, await _factory.WithDbAsync(db => db.TripComments.CountAsync()));
        var azoresTrip = Assert.Single((await FeedAsync(_rui)).Trips);
        Assert.Equal((0, 0), (azoresTrip.Likes, azoresTrip.CommentCount));
    }
}
