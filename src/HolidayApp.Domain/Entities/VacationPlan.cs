namespace HolidayApp.Domain.Entities;

/// <summary>
/// Plan for a vacation (one per entry). The content — destination, bookings,
/// day-by-day plan, tasks and budget — is stored as a JSON document,
/// so adding fields to the plan does not require table changes.
/// </summary>
public sealed class VacationPlan
{
    /// <summary>Entry this plan belongs to (also the key).</summary>
    public Guid EventId { get; set; }

    public required string UserId { get; set; }

    /// <summary>JSON with the <c>VacationPlanDto</c>.</summary>
    public required string Content { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
