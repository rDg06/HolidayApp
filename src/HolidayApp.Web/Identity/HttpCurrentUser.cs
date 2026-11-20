using System.Security.Claims;
using HolidayApp.Domain.Abstractions;

namespace HolidayApp.Web.Identity;

/// <summary>
/// Reads the signed-in account from the session. Without a session (only the sign-in pages
/// get that far) it is nobody: an id that matches no data.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserId =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
}
