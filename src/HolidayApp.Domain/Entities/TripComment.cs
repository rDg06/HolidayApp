namespace HolidayApp.Domain.Entities;

/// <summary>
/// A comment on a trip. Whoever wrote it can delete it, and so can the trip's owner (it is
/// their trip).
/// </summary>
public sealed class TripComment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The trip (vacation entry).</summary>
    public Guid EventId { get; set; }

    /// <summary>Who wrote it (their account's user id).</summary>
    public required string UserId { get; set; }

    public required string Text { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
