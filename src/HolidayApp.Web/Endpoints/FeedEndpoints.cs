using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;

namespace HolidayApp.Web.Endpoints;

/// <summary>The feed of public trips, and the likes and comments on trips.</summary>
public static class FeedEndpoints
{
    public static RouteGroupBuilder MapFeedEndpoints(this RouteGroupBuilder api)
    {
        // GET /api/feed?skip=0 — public trips of everyone, newest first, a page at a time
        api.MapGet("/feed", async (int? skip, IFeedService service, CancellationToken ct) =>
            Results.Ok(await service.GetFeedAsync(skip ?? 0, ct)))
            .WithTags("Feed");

        var trip = api.MapGroup("/events/{id:guid}").WithTags("Feed");

        // PUT /api/events/{id}/like — I like it; DELETE — I no longer like it
        trip.MapPut("/like", async (Guid id, IFeedService service, CancellationToken ct) =>
            await service.SetLikeAsync(id, true, ct) is { } likes ? Results.Ok(likes) : Results.NotFound());
        trip.MapDelete("/like", async (Guid id, IFeedService service, CancellationToken ct) =>
            await service.SetLikeAsync(id, false, ct) is { } likes ? Results.Ok(likes) : Results.NotFound());

        // GET /api/events/{id}/comments — all the comments, oldest first
        trip.MapGet("/comments", async (Guid id, IFeedService service, CancellationToken ct) =>
            await service.GetCommentsAsync(id, ct) is { } comments ? Results.Ok(comments) : Results.NotFound());

        // POST /api/events/{id}/comments
        trip.MapPost("/comments", async (Guid id, AddTripCommentRequest request, IFeedService service, CancellationToken ct) =>
            ToResult(await service.AddCommentAsync(id, request.Text, ct)));

        // DELETE /api/events/{id}/comments/{commentId} — whoever wrote it, or the trip's owner
        trip.MapDelete("/comments/{commentId:guid}", async (Guid id, Guid commentId, IFeedService service, CancellationToken ct) =>
            ToResult(await service.DeleteCommentAsync(id, commentId, ct)));

        return api;
    }

    private static IResult ToResult(CommentResult result) => result.Status switch
    {
        SharingStatus.Ok => result.Comment is { } comment ? Results.Ok(comment) : Results.NoContent(),
        SharingStatus.Forbidden => Results.Problem(title: result.Error, statusCode: StatusCodes.Status403Forbidden),
        SharingStatus.Invalid => Results.ValidationProblem(new Dictionary<string, string[]> { ["Text"] = [result.Error ?? "Invalid."] }),
        _ => Results.NotFound(),
    };
}
