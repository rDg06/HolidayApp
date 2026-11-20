using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>The feed: the trips, the heart, the comments and loading older trips.</summary>
public sealed class FeedTests : BunitContext
{
    private static readonly Guid TripId = Guid.NewGuid();
    private readonly FakeApi _api = new(TripId) { Me = new AccountDto("Ana", "ana@example.com", "ana") };

    public FeedTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/plan-map.js").Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICalendarApi>(_api);
    }

    private static FeedTripDto Trip(string title, int likes = 0, bool likedByMe = false, bool isMine = false,
        int commentCount = 0, params TripCommentDto[] latest) => new(
        Guid.NewGuid(), title, new(2026, 6, 1), new(2026, 6, 8), "São Miguel", new PersonDto("rui", "Rui"), isMine,
        DateTime.UtcNow.AddHours(-3),
        [new PlanPlaceDto { Name = "Sete Cidades", Latitude = 37.86, Longitude = -25.79 }],
        likes, likedByMe, commentCount, latest);

    private static TripCommentDto Comment(string user, string text, bool canDelete = false) =>
        new(Guid.NewGuid(), new PersonDto(user, user), text, DateTime.UtcNow.AddMinutes(-5), canDelete);

    [Fact]
    public void Shows_who_went_where_and_links_to_the_trip()
    {
        _api.FeedTrips.Add(Trip("Azores"));

        var cut = Render<Feed>();
        var post = cut.WaitForElement(".feed-post");

        Assert.Contains("Rui", post.TextContent);
        Assert.Contains("@rui", post.TextContent);
        Assert.Contains("São Miguel", post.TextContent);
        Assert.Contains("3 h", post.QuerySelector(".post-when")!.TextContent);
        Assert.Contains("1 place", post.TextContent);
        Assert.EndsWith("/share?from=feed", post.QuerySelector(".post-open")!.GetAttribute("href"));
        Assert.NotNull(post.QuerySelector(".post-map"));
    }

    [Fact]
    public void My_own_trip_opens_the_plan()
    {
        _api.FeedTrips.Add(Trip("Rome", isMine: true));

        var cut = Render<Feed>();
        var post = cut.WaitForElement(".feed-post");

        Assert.Contains("You", post.QuerySelector(".post-person")!.TextContent);
        Assert.EndsWith("?from=feed", post.QuerySelector(".post-open")!.GetAttribute("href"));
        Assert.DoesNotContain("/share", post.QuerySelector(".post-open")!.GetAttribute("href"));
    }

    [Fact]
    public void The_heart_likes_and_unlikes()
    {
        var trip = Trip("Azores", likes: 4);
        _api.FeedTrips.Add(trip);
        var cut = Render<Feed>();
        cut.WaitForElement(".post-like");

        cut.Find(".post-like").Click();
        Assert.Equal("true", cut.Find(".post-like").GetAttribute("aria-pressed"));
        Assert.Equal("5 likes", cut.Find(".post-count").TextContent);
        Assert.Equal((trip.Id, true), _api.LastLike);

        cut.Find(".post-like").Click();
        Assert.Equal("false", cut.Find(".post-like").GetAttribute("aria-pressed"));
        Assert.Equal("4 likes", cut.Find(".post-count").TextContent);
    }

    [Fact]
    public void A_like_the_server_refuses_goes_back()
    {
        _api.FeedTrips.Add(Trip("Azores", likes: 1));
        _api.FailLikes = true;
        var cut = Render<Feed>();
        cut.WaitForElement(".post-like");

        cut.Find(".post-like").Click();

        Assert.Equal("false", cut.Find(".post-like").GetAttribute("aria-pressed"));
        Assert.Equal("1 like", cut.Find(".post-count").TextContent);
        Assert.Contains("No connection", cut.Find(".form-errors").TextContent);
    }

    [Fact]
    public void Shows_the_newest_comments_and_opens_all_of_them()
    {
        var trip = Trip("Azores", commentCount: 3, latest: [Comment("eva", "Take me next time"), Comment("ana", "Thanks!")]);
        _api.FeedTrips.Add(trip);
        _api.Comments[trip.Id] = [Comment("joao", "First!"), .. trip.LatestComments];
        var cut = Render<Feed>();
        cut.WaitForElement(".comment");

        Assert.Equal(2, cut.FindAll(".comment").Count);
        cut.FindAll("button").Single(b => b.TextContent.Contains("View all 3 comments")).Click();

        Assert.Equal(["First!", "Take me next time", "Thanks!"],
            cut.FindAll(".comment-text").Select(c => c.TextContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last()));
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Contains("View all"));
    }

    [Fact]
    public void Writes_and_deletes_a_comment()
    {
        var trip = Trip("Azores");
        _api.FeedTrips.Add(trip);
        var cut = Render<Feed>();
        cut.WaitForElement(".comment-form");

        cut.Find(".comment-form input").Input("Looks great");
        cut.Find(".comment-form").Submit();

        var comment = cut.Find(".comment");
        Assert.Contains("ana", comment.TextContent);
        Assert.Contains("Looks great", comment.TextContent);
        Assert.Equal("", cut.Find(".comment-form input").GetAttribute("value") ?? "");

        cut.Find(".comment-delete").Click();
        Assert.Empty(cut.FindAll(".comment"));
        Assert.Empty(_api.Comments[trip.Id]);
    }

    [Fact]
    public void Loads_older_trips_on_request()
    {
        _api.FeedPageSize = 2;
        _api.FeedTrips.AddRange([Trip("One"), Trip("Two"), Trip("Three")]);
        var cut = Render<Feed>();
        cut.WaitForElement(".feed-post");

        Assert.Equal(2, cut.FindAll(".feed-post").Count);
        cut.Find(".feed-more").Click();

        Assert.Equal(["One", "Two", "Three"], cut.FindAll(".post-title").Select(t => t.TextContent));
        Assert.Empty(cut.FindAll(".feed-more"));
    }

    [Fact]
    public void Without_public_trips_explains_how_to_add_one()
    {
        var cut = Render<Feed>();
        cut.WaitForElement(".empty-state");

        Assert.Contains("No public trips yet", cut.Find(".empty-state").TextContent);
    }
}
