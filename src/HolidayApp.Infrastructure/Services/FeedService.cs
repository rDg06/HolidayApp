using HolidayApp.Domain.Entities;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared;
using HolidayApp.Shared.Validation;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

/// <summary>
/// The feed (public trips of everyone, newest first) and the likes and comments on trips.
/// Likes and comments work on any trip I can see: public ones, and private ones I was added to.
/// </summary>
public interface IFeedService
{
    /// <summary>A page of the feed: skips the first <paramref name="skip"/> trips.</summary>
    Task<FeedPageDto> GetFeedAsync(int skip, CancellationToken ct = default);

    /// <summary>Likes or unlikes the trip. Null if I cannot see it.</summary>
    Task<TripLikesDto?> SetLikeAsync(Guid eventId, bool like, CancellationToken ct = default);

    /// <summary>All the comments, oldest first. Null if I cannot see the trip.</summary>
    Task<IReadOnlyList<TripCommentDto>?> GetCommentsAsync(Guid eventId, CancellationToken ct = default);

    Task<CommentResult> AddCommentAsync(Guid eventId, string? text, CancellationToken ct = default);

    /// <summary>Whoever wrote it, or the trip's owner, can delete it.</summary>
    Task<CommentResult> DeleteCommentAsync(Guid eventId, Guid commentId, CancellationToken ct = default);
}

/// <summary>Result of writing or deleting a comment (message ready to show when it was not done).</summary>
public sealed record CommentResult(SharingStatus Status, TripCommentDto? Comment = null, string? Error = null)
{
    public static CommentResult NotFound { get; } = new(SharingStatus.NotFound);
}

internal sealed class FeedService(AppDbContext db, TripAccess access) : IFeedService
{
    public const int PageSize = 10;

    /// <summary>Newest comments shown under each trip on the feed (the rest open on request).</summary>
    public const int LatestComments = 2;

    /// <summary>Places sent for each trip's map (enough to see the trip; keeps the feed light).</summary>
    private const int MapPlaces = 60;

    public async Task<FeedPageDto> GetFeedAsync(int skip, CancellationToken ct = default)
    {
        var me = access.Me;
        var trips = await db.Events.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.IsPublic && e.Type == EventType.Vacation)
            .OrderByDescending(e => e.PublishedAtUtc ?? e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip(Math.Max(skip, 0))
            .Take(PageSize + 1) // one more: tells whether there is another page
            .ToListAsync(ct);

        var hasMore = trips.Count > PageSize;
        trips = trips.Take(PageSize).ToList();
        if (trips.Count == 0)
            return new FeedPageDto([], false);

        var ids = trips.Select(e => e.Id).ToList();
        var plans = await db.VacationPlans.IgnoreQueryFilters().AsNoTracking()
            .Where(p => ids.Contains(p.EventId))
            .ToDictionaryAsync(p => p.EventId, p => p.Content, ct);

        var likes = await db.TripLikes.AsNoTracking()
            .Where(l => ids.Contains(l.EventId))
            .GroupBy(l => l.EventId)
            .Select(g => new { g.Key, Count = g.Count(), Mine = g.Any(l => l.UserId == me) })
            .ToDictionaryAsync(x => x.Key, ct);

        var commentCounts = await db.TripComments.AsNoTracking()
            .Where(c => ids.Contains(c.EventId))
            .GroupBy(c => c.EventId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        // The newest comments of each trip (a few small queries: only trips that have comments)
        var latest = new Dictionary<Guid, List<TripComment>>();
        foreach (var id in ids.Where(commentCounts.ContainsKey))
        {
            var newest = await db.TripComments.AsNoTracking()
                .Where(c => c.EventId == id)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(LatestComments)
                .ToListAsync(ct);
            newest.Reverse(); // oldest first, like a conversation
            latest[id] = newest;
        }

        var people = await access.PeopleAsync(
            trips.Select(e => e.UserId).Concat(latest.Values.SelectMany(list => list).Select(c => c.UserId)), ct);

        return new FeedPageDto(trips.Select(e =>
        {
            var plan = plans.TryGetValue(e.Id, out var content) ? VacationPlanService.Deserialize(content) : new VacationPlanDto();
            var like = likes.GetValueOrDefault(e.Id);
            return new FeedTripDto(
                e.Id,
                e.Title,
                e.StartDate,
                e.EndDate,
                plan.Destination,
                people.GetValueOrDefault(e.UserId) ?? TripAccess.Unknown,
                e.UserId == me,
                DateTime.SpecifyKind(e.PublishedAtUtc ?? e.CreatedAtUtc, DateTimeKind.Utc),
                MapOnly(plan.Places),
                like?.Count ?? 0,
                like?.Mine ?? false,
                commentCounts.GetValueOrDefault(e.Id),
                latest.GetValueOrDefault(e.Id)?.Select(c => ToDto(c, e.UserId, people)).ToList() ?? []);
        }).ToList(), hasMore);
    }

    /// <summary>What the map needs (no notes or links: the feed only shows the map).</summary>
    private static List<PlanPlaceDto> MapOnly(List<PlanPlaceDto> places) => places
        .Where(p => p.Latitude != 0 || p.Longitude != 0)
        .Take(MapPlaces)
        .Select(p => new PlanPlaceDto
        {
            Name = p.Name,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            Date = p.Date,
            EndDate = p.EndDate,
            Visited = p.Visited,
            Kind = p.Kind,
        })
        .ToList();

    public async Task<TripLikesDto?> SetLikeAsync(Guid eventId, bool like, CancellationToken ct = default)
    {
        var (ev, _) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return null;

        var me = access.Me;
        if (like)
        {
            if (!await db.TripLikes.AnyAsync(l => l.EventId == eventId && l.UserId == me, ct))
            {
                db.TripLikes.Add(new TripLike { EventId = eventId, UserId = me });
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Liked twice at the same time (two taps): the key keeps only one
                    db.ChangeTracker.Clear();
                }
            }
        }
        else
        {
            await db.TripLikes.Where(l => l.EventId == eventId && l.UserId == me).ExecuteDeleteAsync(ct);
        }

        return new TripLikesDto(
            await db.TripLikes.CountAsync(l => l.EventId == eventId, ct),
            like);
    }

    public async Task<IReadOnlyList<TripCommentDto>?> GetCommentsAsync(Guid eventId, CancellationToken ct = default)
    {
        var (ev, _) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return null;

        var comments = await db.TripComments.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        var people = await access.PeopleAsync(comments.Select(c => c.UserId), ct);
        return comments.Select(c => ToDto(c, ev.UserId, people)).ToList();
    }

    public async Task<CommentResult> AddCommentAsync(Guid eventId, string? text, CancellationToken ct = default)
    {
        var (ev, _) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return CommentResult.NotFound;

        if (TripCommentValidator.Validate(text) is { } error)
            return new CommentResult(SharingStatus.Invalid, Error: error);

        var comment = new TripComment { EventId = eventId, UserId = access.Me, Text = text!.Trim() };
        db.TripComments.Add(comment);
        await db.SaveChangesAsync(ct);

        var people = await access.PeopleAsync([comment.UserId], ct);
        return new CommentResult(SharingStatus.Ok, ToDto(comment, ev.UserId, people));
    }

    public async Task<CommentResult> DeleteCommentAsync(Guid eventId, Guid commentId, CancellationToken ct = default)
    {
        var (ev, _) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return CommentResult.NotFound;

        var comment = await db.TripComments.FirstOrDefaultAsync(c => c.Id == commentId && c.EventId == eventId, ct);
        if (comment is null)
            return CommentResult.NotFound;

        if (comment.UserId != access.Me && ev.UserId != access.Me)
            return new CommentResult(SharingStatus.Forbidden, Error: "Only whoever wrote the comment, or the trip's owner, can delete it.");

        db.TripComments.Remove(comment);
        await db.SaveChangesAsync(ct);
        return new CommentResult(SharingStatus.Ok);
    }

    private TripCommentDto ToDto(TripComment comment, string tripOwnerId, Dictionary<string, PersonDto> people) => new(
        comment.Id,
        people.GetValueOrDefault(comment.UserId) ?? TripAccess.Unknown,
        comment.Text,
        DateTime.SpecifyKind(comment.CreatedAtUtc, DateTimeKind.Utc),
        comment.UserId == access.Me || tripOwnerId == access.Me);
}
