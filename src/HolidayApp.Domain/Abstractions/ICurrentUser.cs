namespace HolidayApp.Domain.Abstractions;

/// <summary>
/// Identifies the user of the current request: the signed-in account or, without
/// accounts, the local user. The rest of the app does not need to know which.
/// </summary>
public interface ICurrentUser
{
    string UserId { get; }
}
