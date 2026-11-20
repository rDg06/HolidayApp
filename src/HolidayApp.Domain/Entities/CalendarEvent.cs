using HolidayApp.Shared;

namespace HolidayApp.Domain.Entities;

/// <summary>A calendar entry (vacation, personal appointment, etc.).</summary>
public sealed class CalendarEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Owner of the entry.</summary>
    public required string UserId { get; set; }

    public required string Title { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public EventType Type { get; set; } = EventType.Vacation;
    public string? Notes { get; set; }

    /// <summary>
    /// Public trip: everyone with an account can see it (read-only). Private by default: only
    /// the owner and the people added to it (<see cref="TripMember"/>).
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>When the trip was last made public: the feed shows the newest first.</summary>
    public DateTime? PublishedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
