namespace HolidayApp.Domain.Entities;

/// <summary>
/// A person added to someone else's trip (a friend you travel with). They see the trip even
/// when it is private and, with <see cref="CanEdit"/>, can change its plan. Only the owner
/// can add or remove people, make the trip public or delete it.
/// </summary>
public sealed class TripMember
{
    /// <summary>The trip (vacation entry).</summary>
    public Guid EventId { get; set; }

    /// <summary>The person added (their account's user id).</summary>
    public required string UserId { get; set; }

    /// <summary>Can change the plan (bookings, places, days, tasks, budget). Otherwise view only.</summary>
    public bool CanEdit { get; set; }

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
