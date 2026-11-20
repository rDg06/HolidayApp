using System.Text.Json;
using HolidayApp.Domain.Entities;
using HolidayApp.Domain.Holidays;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared;
using HolidayApp.Shared.Calendar;
using HolidayApp.Shared.Validation;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

internal sealed class VacationPlanService(
    AppDbContext db,
    TripAccess access,
    IHolidayProvider holidayProvider) : IVacationPlanService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default)
    {
        // Mine and the ones I was added to (by friends who travel with me)
        var me = access.Me;
        var added = await db.TripMembers.AsNoTracking()
            .Where(m => m.UserId == me)
            .ToDictionaryAsync(m => m.EventId, m => m.CanEdit, ct);
        var addedIds = added.Keys.ToList();

        var vacations = await db.Events.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Type == EventType.Vacation && (e.UserId == me || addedIds.Contains(e.Id)))
            .OrderBy(e => e.StartDate)
            .ToListAsync(ct);

        if (vacations.Count == 0)
            return [];

        var ids = vacations.Select(e => e.Id).ToList();
        var plans = await db.VacationPlans.IgnoreQueryFilters().AsNoTracking()
            .Where(p => ids.Contains(p.EventId))
            .ToDictionaryAsync(p => p.EventId, p => Deserialize(p.Content), ct);

        // How many people were added to each trip, and who owns the shared ones
        var peopleCount = await db.TripMembers.AsNoTracking()
            .Where(m => ids.Contains(m.EventId))
            .GroupBy(m => m.EventId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var owners = await access.PeopleAsync(vacations.Where(e => e.UserId != me).Select(e => e.UserId), ct);

        // Holidays of every year covered, to count the working days
        var options = (await db.GetSettingsOrDefaultAsync(ct)).ToHolidayOptions();
        var holidayDates = Enumerable.Range(vacations.Min(e => e.StartDate.Year),
                                            vacations.Max(e => e.EndDate.Year) - vacations.Min(e => e.StartDate.Year) + 1)
            .SelectMany(year => holidayProvider.GetHolidays(year, options))
            .Select(h => h.Date)
            .ToHashSet();

        return vacations.Select(e =>
        {
            var plan = plans.GetValueOrDefault(e.Id) ?? new VacationPlanDto();
            var bookingsTotal = plan.Bookings.Sum(b => b.Cost ?? 0);

            return new VacationSummaryDto(
                e.Id,
                e.Title,
                e.StartDate,
                e.EndDate,
                WorkdayCalculator.CountWorkdays(e.StartDate, e.EndDate, holidayDates),
                plan.Destination,
                plan.Bookings.Count,
                plan.Places.Count(p => p.IsVisit),
                plan.Places.Count(p => p.IsVisit && p.Visited),
                plan.Tasks.Count,
                plan.Tasks.Count(t => t.Done),
                plan.Expenses.Sum(x => x.Planned ?? 0) + bookingsTotal,
                plan.Expenses.Sum(x => x.Actual ?? 0) + bookingsTotal,
                MapPoints(plan, e.Title),
                e.UserId == me ? TripRole.Owner : added[e.Id] ? TripRole.Editor : TripRole.Viewer,
                e.UserId == me ? null : owners.GetValueOrDefault(e.UserId) ?? TripAccess.Unknown,
                peopleCount.GetValueOrDefault(e.Id),
                e.IsPublic);
        }).ToList();
    }

    /// <summary>
    /// Points of the trip on the "Where I've been" map: each lodging (a trip through several
    /// cities gets several) or, without lodging, the centre of the pinned places. Without places
    /// it is empty and the browser looks the destination up by name.
    /// </summary>
    private static List<MapPointDto> MapPoints(VacationPlanDto plan, string title)
    {
        static bool HasCoordinates(PlanPlaceDto p) => p.Latitude != 0 || p.Longitude != 0;

        var lodgings = plan.Places
            .Where(p => p.IsLodging && HasCoordinates(p))
            .OrderBy(p => p.Date ?? DateOnly.MaxValue)
            .Select(p => new MapPointDto(p.Name, p.Latitude, p.Longitude))
            .DistinctBy(p => (Math.Round(p.Latitude, 3), Math.Round(p.Longitude, 3)))
            .ToList();
        if (lodgings.Count > 0)
            return lodgings;

        var places = plan.Places.Where(HasCoordinates).ToList();
        return places.Count == 0
            ? []
            : [new MapPointDto(plan.Destination ?? title, places.Average(p => p.Latitude), places.Average(p => p.Longitude))];
    }

    public async Task<VacationPlanDto?> GetPlanAsync(Guid eventId, CancellationToken ct = default)
    {
        var (ev, role) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return null;

        var entity = await db.VacationPlans.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(p => p.EventId == eventId, ct);
        var plan = entity is null ? new VacationPlanDto() : Deserialize(entity.Content);

        // Booking codes (flight, hotel) let whoever has them change or cancel the booking:
        // strangers who see the trip only because it is public do not get them.
        if (role == TripRole.PublicViewer)
        {
            foreach (var booking in plan.Bookings)
                booking.Reference = null;
        }

        // The entry dates may have changed: days outside the range are no longer shown.
        plan.Days = plan.Days.Where(d => d.Date >= ev.StartDate && d.Date <= ev.EndDate).ToList();
        plan.Version = VersionOf(entity);
        return plan;
    }

    public async Task<SavePlanResult> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default)
    {
        var (ev, role) = await access.FindAsync(eventId, ct);
        if (ev is null)
            return SavePlanResult.NotFound;

        // The owner and the people added with "can edit"
        if (role is not (TripRole.Owner or TripRole.Editor))
            return SavePlanResult.ViewOnly;

        // The entry dates limit the days of bookings and places
        var errors = VacationPlanValidator.Validate(plan, ev.StartDate, ev.EndDate);
        if (errors.Count > 0)
            return SavePlanResult.Invalid(errors);

        var existing = await db.VacationPlans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.EventId == eventId, ct);

        // Someone else saved after this plan was opened: do not overwrite it
        if (plan.Version is { } version && version != VersionOf(existing))
            return SavePlanResult.Outdated;

        var clean = Normalise(plan, ev);
        var content = JsonSerializer.Serialize(clean, Json); // the version is not in the content (it is the save time)
        var savedAt = DateTime.UtcNow;

        void Apply(VacationPlan entity)
        {
            entity.Content = content;
            entity.UpdatedAtUtc = savedAt;
        }

        if (existing is not null)
        {
            Apply(existing);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await db.InsertOrUpdateAsync(
                // The plan belongs to the trip's owner, even when a friend saves it first
                new VacationPlan { EventId = eventId, UserId = ev.UserId, Content = content, UpdatedAtUtc = savedAt },
                token => db.VacationPlans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.EventId == eventId, token),
                Apply,
                ct);
        }

        clean.Version = VersionOf(savedAt);
        return SavePlanResult.Saved(clean);
    }

    /// <summary>
    /// The trip's dates changed: makes the plan fit the new dates, so it can still be saved
    /// (otherwise bookings and places outside the dates block every save until they are fixed
    /// by hand). Nothing is deleted except the day-by-day notes of days that left the trip:
    /// <list type="bullet">
    /// <item>Bookings and places dated outside the trip lose the date (they stay, with no day).</item>
    /// <item>A lodging that still overlaps the trip is cut to the trip's dates.</item>
    /// </list>
    /// Returns the new content, or null when nothing changed.
    /// </summary>
    internal static string? FitToDates(string content, DateOnly start, DateOnly end)
    {
        var plan = Deserialize(content);
        var changed = false;
        bool Outside(DateOnly date) => date < start || date > end;

        foreach (var booking in plan.Bookings.Where(b => b.Date is { } d && Outside(d)))
        {
            booking.Date = null;
            changed = true;
        }

        foreach (var place in plan.Places.Where(p => p.Date is not null))
        {
            var from = place.Date!.Value;
            var to = place.EndDate ?? from;
            if (to < start || from > end)
            {
                place.Date = null;
                place.EndDate = null;
                changed = true;
                continue;
            }

            if (from < start)
            {
                place.Date = start;
                changed = true;
            }
            if (place.EndDate is { } checkOut && checkOut > end)
            {
                place.EndDate = end;
                changed = true;
            }
        }

        var days = plan.Days.Where(d => !Outside(d.Date)).ToList();
        if (days.Count != plan.Days.Count)
        {
            plan.Days = days;
            changed = true;
        }

        return changed ? JsonSerializer.Serialize(plan, Json) : null;
    }

    /// <summary>Version of a plan: the time of the last save (0 if it does not exist yet).</summary>
    private static long VersionOf(VacationPlan? entity) => entity is null ? 0 : VersionOf(entity.UpdatedAtUtc);

    /// <summary>
    /// Milliseconds since 1970: fits in a JavaScript number without rounding (.NET "ticks" do
    /// not, and a JavaScript client could never save).
    /// </summary>
    private static long VersionOf(DateTime savedAtUtc) => (savedAtUtc.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;

    internal static VacationPlanDto Deserialize(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<VacationPlanDto>(content, Json) ?? new VacationPlanDto();
        }
        catch (JsonException)
        {
            // Broken content must not stop the page from opening.
            return new VacationPlanDto();
        }
    }

    /// <summary>Trims spaces, drops empty rows and days outside the entry dates.</summary>
    private static VacationPlanDto Normalise(VacationPlanDto plan, CalendarEvent ev) => new()
    {
        Destination = Clean(plan.Destination),
        RouteMode = plan.RouteMode,
        Bookings = plan.Bookings
            .Select(b => new PlanBookingDto
            {
                Type = b.Type,
                Name = b.Name.Trim(),
                Date = b.Date,
                Reference = Clean(b.Reference),
                Url = Clean(b.Url),
                Notes = Clean(b.Notes),
                Cost = b.Cost,
            })
            .ToList(),
        Days = plan.Days
            .Where(d => d.Date >= ev.StartDate && d.Date <= ev.EndDate)
            .Select(d => new PlanDayDto { Date = d.Date, Place = Clean(d.Place), Activities = Clean(d.Activities) })
            .Where(d => d.Place is not null || d.Activities is not null)
            .DistinctBy(d => d.Date)
            .OrderBy(d => d.Date)
            .ToList(),
        Tasks = plan.Tasks
            .Select(t => new PlanTaskDto { Text = t.Text.Trim(), Done = t.Done })
            .ToList(),
        Expenses = plan.Expenses
            .Select(e => new PlanExpenseDto
            {
                Category = e.Category,
                Description = Clean(e.Description),
                Planned = e.Planned,
                Actual = e.Actual,
            })
            .ToList(),
        Places = plan.Places
            .Select(p => new PlanPlaceDto
            {
                Name = p.Name.Trim(),
                // 6 decimal places ≈ 10 cm: enough, and avoids storing noise
                Latitude = Math.Round(p.Latitude, 6),
                Longitude = Math.Round(p.Longitude, 6),
                // Lodging has check-in and check-out (no check-out = a single day) and no "visited"
                Date = p.Date,
                EndDate = p.IsLodging && p.Date is not null ? p.EndDate ?? p.Date : null,
                Notes = Clean(p.Notes),
                Url = Clean(p.Url),
                Visited = p.IsVisit && p.Visited, // only places to visit have "visited"

                Kind = p.Kind,
            })
            .ToList(),
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
