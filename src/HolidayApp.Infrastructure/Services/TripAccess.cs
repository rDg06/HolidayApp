using HolidayApp.Domain.Abstractions;
using HolidayApp.Domain.Entities;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

/// <summary>
/// Who can see and change a trip. The global filters only show your own data; trips you were
/// added to and public trips go through here, which looks past the filters and checks the rules:
/// <list type="bullet">
/// <item>Owner: everything.</item>
/// <item>Added with "can edit": sees it and changes the plan.</item>
/// <item>Added with "view only", or anyone with an account when the trip is public: sees it.</item>
/// </list>
/// Only vacations are trips: other entries are never shared.
/// </summary>
internal sealed class TripAccess(AppDbContext db, ICurrentUser currentUser)
{
    public string Me => currentUser.UserId;

    /// <summary>
    /// The trip and what I can do with it. A trip I cannot see comes back as (null, None),
    /// the same as one that does not exist (does not reveal other people's trips).
    /// </summary>
    public async Task<(CalendarEvent? Event, TripRole Role)> FindAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await db.Events.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return (null, TripRole.None);

        if (ev.UserId == Me)
            return (ev, TripRole.Owner);

        if (ev.Type != EventType.Vacation)
            return (null, TripRole.None);

        var member = await db.TripMembers.AsNoTracking().FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == Me, ct);
        if (member is not null)
            return (ev, member.CanEdit ? TripRole.Editor : TripRole.Viewer);

        return ev.IsPublic ? (ev, TripRole.PublicViewer) : (null, TripRole.None);
    }

    /// <summary>Accounts by user id (people shown next to trips), without emails.</summary>
    public async Task<Dictionary<string, PersonDto>> PeopleAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var ids = userIds.Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null).OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var accounts = await db.Accounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        return accounts.ToDictionary(a => a.UserId, ToPerson);
    }

    public static PersonDto ToPerson(UserAccount account) => new(account.Username ?? "", account.Name);

    /// <summary>Someone whose account no longer exists (or the data from before accounts).</summary>
    public static PersonDto Unknown { get; } = new("", "Someone");
}
