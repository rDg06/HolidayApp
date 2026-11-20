namespace HolidayApp.Domain.Entities;

/// <summary>
/// A person's account (email and password). The <see cref="UserId"/> is what every other
/// table stores to know whose data it is.
/// </summary>
public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Identifier used in the data (entries, plans, settings…).</summary>
    public string UserId => Id.ToString();

    /// <summary>Lower-case email (it is the user name; unique).</summary>
    public required string Email { get; set; }

    /// <summary>Display name (e.g. "Ana").</summary>
    public required string Name { get; set; }

    /// <summary>Password stored only as a hash (PBKDF2), never as text.</summary>
    public required string PasswordHash { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
