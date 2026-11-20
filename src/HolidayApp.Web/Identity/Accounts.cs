using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HolidayApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace HolidayApp.Web.Identity;

/// <summary>
/// Accounts with email and password (always on). Creating an account needs the invite code
/// ("Auth:InviteCode"; the older "Auth:GroupPassword" works too); without a code configured
/// nobody can create accounts. Without a session only the sign-in and register pages open; each
/// person sees their own data, the trips they were added to and public trips. The first account
/// created gets the data that already existed (see AccountService).
/// </summary>
public static class Accounts
{
    public const string SchemeName = "Accounts";
    public const string LoginPath = "/login";
    public const string RegisterPath = "/register";
    public const string LogoutPath = "/logout";
    public const string AccountIdClaim = "account_id";

    /// <summary>
    /// Fingerprint of the password the session was opened with. Changing the password changes
    /// it, so the sessions on other devices end (someone who knew the old password is out).
    /// </summary>
    private const string PasswordStampClaim = "password_stamp";
    private const string RateLimitPolicy = "accounts";

    /// <summary>
    /// Paths open without a session: login, register, logout, the icon the pages show and the
    /// service worker (nothing personal; if it were closed, the browser could not update it
    /// and an old version could get stuck and break the app).
    /// </summary>
    private static readonly string[] OpenPaths = [LoginPath, RegisterPath, LogoutPath, "/favicon.svg", "/service-worker.js"];

    public static string? InviteCode(IConfiguration configuration) =>
        configuration["Auth:InviteCode"] is { Length: > 0 } code ? code
        : configuration["Auth:GroupPassword"] is { Length: > 0 } old ? old
        : null;

    /// <summary>Sign-in with the session cookie, the limit on attempts and authorization.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.AddAuthentication(SchemeName).AddAccountCookie();
        services.AddAccountRateLimit();
        services.AddAuthorization();
        return services;
    }

    public static AuthenticationBuilder AddAccountCookie(this AuthenticationBuilder builder) =>
        builder.AddCookie(SchemeName, options =>
        {
            options.Cookie.Name = "holidayapp.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;

            // Account deleted (on another device), or the password changed since this session
            // started: the session is no longer valid
            options.Events.OnValidatePrincipal = async context =>
            {
                var id = context.Principal?.FindFirstValue(AccountIdClaim);
                var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
                var account = Guid.TryParse(id, out var accountId) ? await accounts.FindAsync(accountId, context.HttpContext.RequestAborted) : null;
                if (account is null || context.Principal?.FindFirstValue(PasswordStampClaim) != PasswordStamp(account))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(SchemeName);
                }
            };
        });

    /// <summary>
    /// At most 10 sign-in or register attempts per minute from each address (against guessing).
    /// Counted per address: with a single shared count, one person guessing would lock everyone
    /// else out of signing in.
    /// </summary>
    public static IServiceCollection AddAccountRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicy, context =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

    /// <summary>Without a session: the API answers 401 and everything else goes to the login page.</summary>
    public static IApplicationBuilder UseAccountGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (context.User.Identity?.IsAuthenticated == true || OpenPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)))
            {
                await next(context);
                return;
            }

            if (path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            context.Response.Redirect($"{LoginPath}?returnUrl={Uri.EscapeDataString(path + context.Request.QueryString)}");
        });

    public static void MapAccountPages(this IEndpointRouteBuilder app)
    {
        app.MapGet(LoginPath, (string? returnUrl) =>
            Page(LoginForm(SafeReturnUrl(returnUrl), email: null, errors: [])));

        app.MapPost(LoginPath, async (HttpContext context, IAccountService accounts) =>
        {
            var form = await context.Request.ReadFormAsync();
            var back = SafeReturnUrl(form["returnUrl"]);
            var account = await accounts.SignInAsync(form["email"], form["password"], context.RequestAborted);
            if (account is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)); // slows down guessing
                return Page(LoginForm(back, form["email"], ["Wrong email, user name or password."]), StatusCodes.Status400BadRequest);
            }

            await SignInAsync(context, account);
            return Results.Redirect(back);
        })
        .DisableAntiforgery() // plain forms, no Blazor
        .RequireRateLimiting(RateLimitPolicy);

        app.MapGet(RegisterPath, (string? returnUrl, IConfiguration configuration) =>
            InviteCode(configuration) is null
                ? Page(RegistrationClosed(SafeReturnUrl(returnUrl)))
                : Page(RegisterForm(SafeReturnUrl(returnUrl), name: null, email: null, username: null, errors: [])));

        app.MapPost(RegisterPath, async (HttpContext context, IAccountService accounts, IConfiguration configuration) =>
        {
            var form = await context.Request.ReadFormAsync();
            var back = SafeReturnUrl(form["returnUrl"]);
            string name = form["name"].ToString(), email = form["email"].ToString(), username = form["username"].ToString();

            // Without an invite code configured nobody can create accounts
            if (InviteCode(configuration) is not { } code)
                return Page(RegistrationClosed(back), StatusCodes.Status403Forbidden);

            if (!Matches(form["inviteCode"].ToString(), code))
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return Page(RegisterForm(back, name, email, username, ["The invite code is wrong. Ask the person who invited you."]), StatusCodes.Status400BadRequest);
            }

            var (account, errors) = await accounts.RegisterAsync(name, email, form["password"], username, context.RequestAborted);
            if (account is null)
                return Page(RegisterForm(back, name, email, username, errors), StatusCodes.Status400BadRequest);

            await SignInAsync(context, account);
            return Results.Redirect(back);
        })
        .DisableAntiforgery()
        .RequireRateLimiting(RateLimitPolicy);

        // Signing out is a POST: a link or image on another site (GET) cannot sign you out, and
        // the session cookie (SameSite=Lax) does not go with POSTs that come from other sites.
        app.MapPost(LogoutPath, async (HttpContext context) =>
        {
            await context.SignOutAsync(SchemeName);
            return Results.Redirect(LoginPath);
        })
        .DisableAntiforgery(); // plain form, no Blazor

        // Opening /logout (an old link or bookmark) asks first, with a button that signs out
        app.MapGet(LogoutPath, (HttpContext context) =>
            context.User.Identity?.IsAuthenticated == true
                ? Page(LogoutForm(), title: "Sign out", clearOfflineData: false)
                : Results.Redirect(LoginPath));
    }

    /// <summary>Fingerprint of the current password (part of a hash of the hash: reveals nothing).</summary>
    private static string PasswordStamp(Domain.Entities.UserAccount account) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account.PasswordHash)))[..16];

    public static async Task SignInAsync(HttpContext context, Domain.Entities.UserAccount account)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, account.UserId),
            new Claim(AccountIdClaim, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Name),
            new Claim(ClaimTypes.Email, account.Email),
            new Claim(PasswordStampClaim, PasswordStamp(account)),
        ], SchemeName);
        await context.SignInAsync(SchemeName, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
    }

    /// <summary>Compares in constant time (the code cannot be guessed from the response time).</summary>
    private static bool Matches(string attempt, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(attempt)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    /// <summary>Only returns to pages of the app itself (never another site, nor the sign-in pages).</summary>
    private static string SafeReturnUrl(string? url) =>
        url is { Length: > 0 } && url[0] == '/' && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal)
            && !url.StartsWith(LoginPath, StringComparison.OrdinalIgnoreCase) && !url.StartsWith(RegisterPath, StringComparison.OrdinalIgnoreCase)
            ? url
            : "/";

    // ---------- Pages (plain HTML, no Blazor: they open before there is a session) ----------

    /// <summary>Escapes text for HTML, but leaves accents as they are (the page is UTF-8).</summary>
    private static readonly System.Text.Encodings.Web.HtmlEncoder Html =
        System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All);

    private static string E(string? text) => Html.Encode(text ?? "");

    private static string Errors(IEnumerable<string> errors) =>
        string.Concat(errors.Select(e => $"""<p class="error" role="alert">{E(e)}</p>"""));

    private static string LoginForm(string back, string? email, IReadOnlyList<string> errors) => $$"""
        <form method="post" action="{{LoginPath}}">
            <div class="brand"><img src="/favicon.svg" alt="" width="28" height="28" /> HolidayApp</div>
            <p>Sign in to your account.</p>
            {{Errors(errors)}}
            <label>Email or user name
                <input type="text" name="email" value="{{E(email)}}" autocomplete="username" autocapitalize="none" spellcheck="false" required autofocus />
            </label>
            <label>Password
                <input type="password" name="password" autocomplete="current-password" required />
            </label>
            <input type="hidden" name="returnUrl" value="{{E(back)}}" />
            <button type="submit">Sign in</button>
            <p class="switch">No account yet? <a href="{{RegisterPath}}?returnUrl={{Uri.EscapeDataString(back)}}">Create account</a></p>
        </form>
        """;

    private static string RegistrationClosed(string back) => $$"""
        <form>
            <div class="brand"><img src="/favicon.svg" alt="" width="28" height="28" /> HolidayApp</div>
            <p>Creating accounts is closed: this server has no invite code yet. Ask the person who runs it.</p>
            <p class="switch">Already have an account? <a href="{{LoginPath}}?returnUrl={{Uri.EscapeDataString(back)}}">Sign in</a></p>
        </form>
        """;

    private static string RegisterForm(string back, string? name, string? email, string? username, IReadOnlyList<string> errors) => $$"""
        <form method="post" action="{{RegisterPath}}">
            <div class="brand"><img src="/favicon.svg" alt="" width="28" height="28" /> HolidayApp</div>
            <p>Create your account. You need the invite code from the person who invited you.</p>
            {{Errors(errors)}}
            <label>Name
                <input type="text" name="name" value="{{E(name)}}" maxlength="60" autocomplete="name" required autofocus />
            </label>
            <label>Email
                <input type="email" name="email" value="{{E(email)}}" maxlength="254" autocomplete="email" required />
            </label>
            <label>User name <span class="hint">(how friends find you to add you to a trip; empty = from your email)</span>
                <input type="text" name="username" value="{{E(username)}}" minlength="3" maxlength="30" pattern="@?[A-Za-z0-9][A-Za-z0-9._]*"
                       autocapitalize="none" spellcheck="false" autocomplete="off" placeholder="e.g. ana.silva" />
            </label>
            <label>Password <span class="hint">(at least 8 characters)</span>
                <input type="password" name="password" minlength="8" maxlength="128" autocomplete="new-password" required />
            </label>
            <label>Invite code
                <input type="password" name="inviteCode" autocomplete="off" required />
            </label>
            <input type="hidden" name="returnUrl" value="{{E(back)}}" />
            <button type="submit">Create account</button>
            <p class="switch">Already have an account? <a href="{{LoginPath}}?returnUrl={{Uri.EscapeDataString(back)}}">Sign in</a></p>
        </form>
        """;

    /// <summary>
    /// No session: deletes what the app stored on this device to work offline (the previous
    /// account's data, old versions) and the service worker. After signing in, the app
    /// registers it again and stores things again.
    /// </summary>
    private const string ClearOfflineDataScript = """
        <script>
            if ("serviceWorker" in navigator) navigator.serviceWorker.getRegistrations().then((all) => all.forEach((r) => r.unregister()));
            if ("caches" in window) caches.keys().then((names) => names.forEach((name) => caches.delete(name)));
        </script>
        """;

    private static string LogoutForm() => $$"""
        <form method="post" action="{{LogoutPath}}">
            <div class="brand"><img src="/favicon.svg" alt="" width="28" height="28" /> HolidayApp</div>
            <p>Sign out of HolidayApp on this device?</p>
            <button type="submit">Sign out</button>
            <p class="switch"><a href="/">Back to the app</a></p>
        </form>
        """;

    /// <param name="clearOfflineData">
    /// Deletes what the app stored on this device (pages without a session). Not on the
    /// "Sign out?" page: the person is still signed in and may go back to the app.
    /// </param>
    private static IResult Page(string form, int status = StatusCodes.Status200OK, string title = "Sign in", bool clearOfflineData = true) => Results.Content($$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <meta name="robots" content="noindex" />
            <title>{{E(title)}} · HolidayApp</title>
            <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
            <style>
                :root { --bg: #f6f5f1; --card: #fff; --text: #1c1b18; --muted: #77736a; --border: #e5e2da; --primary: #0f766e; --on-primary: #fff; --danger: #c8323a; color-scheme: light; }
                @media (prefers-color-scheme: dark) {
                    :root { --bg: #141413; --card: #1d1d1b; --text: #eceae4; --muted: #9a968c; --border: #33322f; --primary: #2cc4ad; --on-primary: #0b1f1c; --danger: #ff6b6f; color-scheme: dark; }
                }
                * { box-sizing: border-box; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 16px; background: var(--bg); color: var(--text);
                       font: 15px/1.45 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
                form { width: 100%; max-width: 360px; display: grid; gap: 14px; padding: 28px; border: 1px solid var(--border); border-radius: 16px; background: var(--card); }
                .brand { display: flex; align-items: center; gap: 10px; font-weight: 700; font-size: 1.2rem; }
                p { margin: 0; color: var(--muted); }
                label { display: grid; gap: 6px; font-weight: 500; font-size: 0.9rem; }
                .hint { font-weight: 400; color: var(--muted); }
                input { height: 42px; padding: 0 12px; border: 1px solid var(--border); border-radius: 10px; background: var(--card); color: var(--text); font: inherit; }
                input:focus { outline: 2px solid var(--primary); outline-offset: -1px; border-color: transparent; }
                button { height: 42px; border: 0; border-radius: 10px; background: var(--primary); color: var(--on-primary); font: inherit; font-weight: 600; cursor: pointer; }
                .error { color: var(--danger); font-weight: 500; }
                .switch { text-align: center; font-size: 0.9rem; }
                .switch a { color: var(--primary); font-weight: 600; }
            </style>
        </head>
        <body>
            {{form}}
            {{(clearOfflineData ? ClearOfflineDataScript : "")}}
        </body>
        </html>
        """, "text/html; charset=utf-8", statusCode: status);
}
