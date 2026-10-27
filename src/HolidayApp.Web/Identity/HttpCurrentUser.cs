using System.Security.Claims;
using HolidayApp.Domain.Abstractions;

namespace HolidayApp.Web.Identity;

/// <summary>
/// Lê o utilizador das claims de autenticação. Enquanto não houver login,
/// todos os pedidos pertencem ao utilizador "local".
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string LocalUserId = "local";

    public string UserId =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? LocalUserId;
}
