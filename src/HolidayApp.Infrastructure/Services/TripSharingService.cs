using System.Text.Json;
using HolidayApp.Domain.Entities;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared;
using HolidayApp.Shared.Validation;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Public or private trips, the people added to each trip and the "Explore" page.</summary>
public interface ITripSharingService
{
    /// <summary>Who sees the trip (null if I cannot see it).</summary>
    Task<TripSharingDto?> GetAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Public or private (owner only).</summary>
    Task<SharingResult> SetPublicAsync(Guid eventId, bool isPublic, CancellationToken ct = default);

    /// <summary>Adds someone by email or user name (owner only).</summary>
    Task<SharingResult> AddMemberAsync(Guid eventId, string? person, bool canEdit, CancellationToken ct = default);

    /// <summary>"Can edit" or "view only" (owner only).</summary>
    Task<SharingResult> SetMemberAsync(Guid eventId, string username, bool canEdit, CancellationToken ct = default);

    /// <summary>The owner removes someone, or someone leaves the trip (their own user name).</summary>
    Task<SharingResult> RemoveMemberAsync(Guid eventId, string username, CancellationToken ct = default);

    /// <summary>Public trips of everyone, most recent first.</summary>
    Task<IReadOnlyList<PublicTripDto>> GetPublicTripsAsync(CancellationToken ct = default);
}

public enum SharingStatus { Ok, NotFound, Forbidden, Invalid }

/// <summary>Result of a change: the new state, or why it was not done (message ready to show).</summary>
public sealed record SharingResult(SharingStatus Status, TripSharingDto? Sharing = null, string? Error = null)
{
    public static SharingResult NotFound { get; } = new(SharingStatus.NotFound);
    public static SharingResult OnlyOwner { get; } = new(SharingStatus.Forbidden, Error: "Only the person who created the trip can do this.");
    public static SharingResult Invalid(string error) => new(SharingStatus.Invalid, Error: error);
}

internal sealed class TripSharingService(AppDbContext db, TripAccess access) : ITripSharingService
{
    private const int ExploreLimit = 100;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<TripSharingDto?> GetAsync(Guid eventId, CancellationToken ct = default)
    {
        var (ev, role) = await access.FindAsync(eventId, ct);
        return ev is null ? null : await BuildAsync(ev, role, ct);
    }

    public async Task<SharingResult> SetPublicAsync(Guid eventId, bool isPublic, CancellationToken ct = default)
    {
        var (ev, role) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return SharingResult.NotFound;
        if (role != TripRole.Owner)
            return SharingResult.OnlyOwner;
        if (ev.Type != EventType.Vacation)
            return SharingResult.Invalid("Only vacations can be shared.");

        await db.Events.IgnoreQueryFilters().Where(e => e.Id == eventId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsPublic, isPublic), ct);
        ev.IsPublic = isPublic;
        return new(SharingStatus.Ok, await BuildAsync(ev, role, ct));
    }

    public async Task<SharingResult> AddMemberAsync(Guid eventId, string? person, bool canEdit, CancellationToken ct = default)
    {
        var (ev, role) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return SharingResult.NotFound;
        if (role != TripRole.Owner)
            return SharingResult.OnlyOwner;
        if (ev.Type != EventType.Vacation)
            return SharingResult.Invalid("Only vacations can be shared.");

        var account = await FindPersonAsync(person, ct);
        if (account is null)
            return SharingResult.Invalid(string.IsNullOrWhiteSpace(person)
                ? "Write the email or user name of the person to add."
                : "There is no account with that email or user name. They need to create one first (with the invite code).");
        if (account.UserId == ev.UserId)
            return SharingResult.Invalid("That is you: the trip is already yours.");
        if (await db.TripMembers.AnyAsync(m => m.EventId == eventId && m.UserId == account.UserId, ct))
            return SharingResult.Invalid($"{account.Name} is already in this trip.");

        db.TripMembers.Add(new TripMember { EventId = eventId, UserId = account.UserId, CanEdit = canEdit });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Added twice at the same time: the key stops the second
            db.ChangeTracker.Clear();
        }

        return new(SharingStatus.Ok, await BuildAsync(ev, role, ct));
    }

    public async Task<SharingResult> SetMemberAsync(Guid eventId, string username, bool canEdit, CancellationToken ct = default)
    {
        var (ev, role) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return SharingResult.NotFound;
        if (role != TripRole.Owner)
            return SharingResult.OnlyOwner;

        var userId = await UserIdOfAsync(username, ct);
        var changed = userId is not null && await db.TripMembers
            .Where(m => m.EventId == eventId && m.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CanEdit, canEdit), ct) > 0;

        return changed ? new(SharingStatus.Ok, await BuildAsync(ev, role, ct)) : SharingResult.Invalid("That person is not in this trip.");
    }

    public async Task<SharingResult> RemoveMemberAsync(Guid eventId, string username, CancellationToken ct = default)
    {
        var (ev, role) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return SharingResult.NotFound;

        var userId = await UserIdOfAsync(username, ct);
        var leaving = userId is not null && userId == access.Me;
        if (role != TripRole.Owner && !leaving)
            return SharingResult.OnlyOwner;

        var removed = userId is not null && await db.TripMembers
            .Where(m => m.EventId == eventId && m.UserId == userId)
            .ExecuteDeleteAsync(ct) > 0;
        if (!removed)
            return SharingResult.Invalid("That person is not in this trip.");

        // Whoever left may now only see it if it is public
        var (_, newRole) = await access.FindAsync(eventId, ct);
        return new(SharingStatus.Ok, newRole == TripRole.None ? null : await BuildAsync(ev, newRole, ct));
    }

    public async Task<IReadOnlyList<PublicTripDto>> GetPublicTripsAsync(CancellationToken ct = default)
    {
        var trips = await db.Events.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.IsPublic && e.Type == EventType.Vacation)
            .OrderByDescending(e => e.StartDate)
            .Take(ExploreLimit)
            .ToListAsync(ct);
        if (trips.Count == 0)
            return [];

        var ids = trips.Select(e => e.Id).ToList();
        var plans = await db.VacationPlans.IgnoreQueryFilters().AsNoTracking()
            .Where(p => ids.Contains(p.EventId))
            .ToDictionaryAsync(p => p.EventId, p => p.Content, ct);
        var people = await access.PeopleAsync(trips.Select(e => e.UserId), ct);

        return trips.Select(e =>
        {
            var plan = plans.TryGetValue(e.Id, out var content) ? Deserialize(content) : new VacationPlanDto();
            return new PublicTripDto(
                e.Id,
                e.Title,
                e.StartDate,
                e.EndDate,
                plan.Destination,
                plan.Places.Count(p => p.IsVisit),
                people.GetValueOrDefault(e.UserId) ?? TripAccess.Unknown,
                e.UserId == access.Me);
        }).ToList();
    }

    // ---------- Helpers ----------

    private async Task<TripSharingDto> BuildAsync(CalendarEvent ev, TripRole role, CancellationToken ct)
    {
        var members = await db.TripMembers.AsNoTracking()
            .Where(m => m.EventId == ev.Id)
            .OrderBy(m => m.AddedAtUtc)
            .ToListAsync(ct);
        var people = await access.PeopleAsync(members.Select(m => m.UserId).Append(ev.UserId), ct);

        // Someone who only sees it because it is public does not get the list of people
        var list = role == TripRole.PublicViewer
            ? []
            : members.Where(m => people.ContainsKey(m.UserId))
                .Select(m => new TripMemberDto(people[m.UserId].Username, people[m.UserId].Name, m.CanEdit))
                .ToList();

        return new TripSharingDto(role, ev.IsPublic, people.GetValueOrDefault(ev.UserId) ?? TripAccess.Unknown, list);
    }

    /// <summary>By email ("ana@mail.com") or user name ("ana.silva" or "@ana.silva").</summary>
    private Task<UserAccount?> FindPersonAsync(string? person, CancellationToken ct)
    {
        var text = (person ?? "").Trim();
        if (text.Length == 0)
            return Task.FromResult<UserAccount?>(null);

        if (text.Contains('@') && !text.StartsWith('@'))
        {
            var email = AccountValidator.NormaliseEmail(text);
            return db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Email == email, ct);
        }

        var username = AccountValidator.NormaliseUsername(text);
        return db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Username == username, ct);
    }

    private async Task<string?> UserIdOfAsync(string username, CancellationToken ct)
    {
        var clean = AccountValidator.NormaliseUsername(username);
        var id = await db.Accounts.AsNoTracking().Where(a => a.Username == clean).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        return id?.ToString();
    }

    private static VacationPlanDto Deserialize(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<VacationPlanDto>(content, Json) ?? new VacationPlanDto();
        }
        catch (JsonException)
        {
            return new VacationPlanDto();
        }
    }
}
