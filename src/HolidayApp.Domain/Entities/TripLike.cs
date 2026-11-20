namespace HolidayApp.Domain.Entities;

/// <summary>Someone liked a trip (the heart on the feed). One per person and trip.</summary>
public sealed class TripLike
{
    /// <summary>The trip (vacation entry).</summary>
    public Guid EventId { get; set; }

    /// <summary>Who liked it (their account's user id).</summary>
    public required string UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
