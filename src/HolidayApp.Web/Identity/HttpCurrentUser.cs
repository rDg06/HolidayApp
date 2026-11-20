using System.Security.Claims;
using HolidayApp.Domain.Abstractions;

namespace HolidayApp.Web.Identity;

/// <summary>
/// Reads the user from the authentication claims. Without a sign-in,
/// every request belongs to the "local" user.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string LocalUserId = "local";

    public string UserId =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? LocalUserId;
}
