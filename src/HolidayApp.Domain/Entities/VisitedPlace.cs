namespace HolidayApp.Domain.Entities;

/// <summary>
/// A place you have been to, added by hand on the "Where I've been" map (trips that are not
/// in the calendar, for example from years before using the app).
/// </summary>
public sealed class VisitedPlace
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string UserId { get; set; }

    public required string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Country, from the place search.</summary>
    public string? Country { get; set; }

    /// <summary>Year you were there.</summary>
    public int Year { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
