using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HolidayApp.Web.Identity;

/// <summary>
/// Palavra-passe do grupo ("Auth:GroupPassword"): para abrir a app a amigos pela internet
/// (ex.: Tailscale Funnel) sem contas. Quem não entrou só vê a página de entrada; quem entra
/// fica com sessão de 30 dias neste aparelho e vê os mesmos dados que todo o grupo
/// (utilizador "local", o mesmo de antes).
/// </summary>
public static class GroupPassword
{
    public const string SchemeName = "GroupPassword";
    public const string LoginPath = "/entrar";
    private const string RateLimitPolicy = "entrar";

    /// <summary>Caminhos abertos sem sessão: a página de entrada e o ícone que ela mostra.</summary>
    private static readonly string[] OpenPaths = [LoginPath, "/sair", "/favicon.svg"];

    public static bool IsEnabled(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Auth:GroupPassword"]);

    public static AuthenticationBuilder AddGroupPasswordCookie(this AuthenticationBuilder builder) =>
        builder.AddCookie(SchemeName, options =>
        {
            options.Cookie.Name = "holidayapp.sessao";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
        });

    /// <summary>No máximo 10 tentativas de entrada por minuto (contra quem tenta adivinhar).</summary>
    public static IServiceCollection AddGroupPasswordRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter(RateLimitPolicy, limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
        });

    /// <summary>
    /// Sem sessão, tudo fica fechado: a API responde 401 e as páginas (e os ficheiros da app)
    /// mandam para a página de entrada. Só corre com "Auth:GroupPassword" definido.
    /// </summary>
    public static IApplicationBuilder UseGroupPasswordGate(this IApplicationBuilder app) =>
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

            var back = path + context.Request.QueryString;
            context.Response.Redirect($"{LoginPath}?volta={Uri.EscapeDataString(back)}");
        });

    public static void MapGroupPasswordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(LoginPath, (string? volta, bool? erro) =>
            Results.Content(LoginPage(SafeReturnUrl(volta), erro == true), "text/html; charset=utf-8"));

        app.MapPost(LoginPath, async (HttpContext context, IConfiguration configuration) =>
        {
            var form = await context.Request.ReadFormAsync();
            var back = SafeReturnUrl(form["volta"]);

            if (!Matches(form["senha"].ToString(), configuration["Auth:GroupPassword"]!))
            {
                await Task.Delay(TimeSpan.FromSeconds(1)); // abranda quem tenta adivinhar
                return Results.Redirect($"{LoginPath}?erro=true&volta={Uri.EscapeDataString(back)}");
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, HttpCurrentUser.LocalUserId), new Claim(ClaimTypes.Name, "Grupo")],
                SchemeName);
            await context.SignInAsync(SchemeName, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
            return Results.Redirect(back);
        })
        .DisableAntiforgery() // formulário próprio, sem Blazor; o risco (entrar na sessão de outro) é nulo aqui
        .RequireRateLimiting(RateLimitPolicy);

        app.MapGet("/sair", async (HttpContext context) =>
        {
            await context.SignOutAsync(SchemeName);
            return Results.Redirect(LoginPath);
        });
    }

    /// <summary>Compara em tempo constante (não deixa adivinhar a palavra-passe pelo tempo de resposta).</summary>
    private static bool Matches(string attempt, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(attempt)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    /// <summary>Só volta para páginas da própria app (nunca para outro site).</summary>
    private static string SafeReturnUrl(string? url) =>
        url is { Length: > 0 } && url[0] == '/' && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal)
            && !url.StartsWith(LoginPath, StringComparison.OrdinalIgnoreCase)
            ? url
            : "/";

    private static string LoginPage(string back, bool error) => $$"""
        <!DOCTYPE html>
        <html lang="pt-PT">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <meta name="robots" content="noindex" />
            <title>Entrar · HolidayApp</title>
            <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
            <style>
                :root { --bg: #f6f5f1; --card: #fff; --text: #1c1b18; --muted: #77736a; --border: #e5e2da; --primary: #0f766e; --on-primary: #fff; --danger: #c8323a; color-scheme: light; }
                @media (prefers-color-scheme: dark) {
                    :root { --bg: #141413; --card: #1d1d1b; --text: #eceae4; --muted: #9a968c; --border: #33322f; --primary: #2cc4ad; --on-primary: #0b1f1c; --danger: #ff6b6f; color-scheme: dark; }
                }
                * { box-sizing: border-box; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 16px; background: var(--bg); color: var(--text);
                       font: 15px/1.45 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
                form { width: 100%; max-width: 340px; display: grid; gap: 14px; padding: 28px; border: 1px solid var(--border); border-radius: 16px; background: var(--card); }
                .brand { display: flex; align-items: center; gap: 10px; font-weight: 700; font-size: 1.2rem; }
                p { margin: 0; color: var(--muted); }
                label { display: grid; gap: 6px; font-weight: 500; font-size: 0.9rem; }
                input { height: 42px; padding: 0 12px; border: 1px solid var(--border); border-radius: 10px; background: var(--card); color: var(--text); font: inherit; }
                input:focus { outline: 2px solid var(--primary); outline-offset: -1px; border-color: transparent; }
                button { height: 42px; border: 0; border-radius: 10px; background: var(--primary); color: var(--on-primary); font: inherit; font-weight: 600; cursor: pointer; }
                .error { color: var(--danger); font-weight: 500; }
            </style>
        </head>
        <body>
            <form method="post" action="{{LoginPath}}">
                <div class="brand"><img src="/favicon.svg" alt="" width="28" height="28" /> HolidayApp</div>
                <p>Escreve a palavra-passe do grupo para entrar.</p>
                {{(error ? """<p class="error" role="alert">Palavra-passe errada. Tenta outra vez.</p>""" : "")}}
                <label>Palavra-passe
                    <input type="password" name="senha" autocomplete="current-password" required autofocus />
                </label>
                <input type="hidden" name="volta" value="{{WebUtility.HtmlEncode(back)}}" />
                <button type="submit">Entrar</button>
            </form>
        </body>
        </html>
        """;
}
