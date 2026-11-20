using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HolidayApp.Web.Identity;

/// <summary>
/// Reads the user signed in by Azure App Service "Authentication" (Easy Auth).
/// App Service does the sign-in (Microsoft, Google…) before the request reaches the app and
/// sends who it is in the X-MS-CLIENT-PRINCIPAL-* headers. Those headers can only be
/// trusted when App Service really is in front (WEBSITE_AUTH_ENABLED=True),
/// because it removes any that came from the browser.
/// </summary>
public sealed class AppServiceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AppService";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!string.Equals(configuration["WEBSITE_AUTH_ENABLED"], "true", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var id = Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"].ToString();
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(AuthenticateResult.NoResult());

        var provider = Request.Headers["X-MS-CLIENT-PRINCIPAL-IDP"].ToString();
        var name = Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].ToString();

        var claims = new List<Claim>
        {
            // The provider is part of the id: the same id on different providers is different people
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(provider) ? id : $"{provider}:{id}"),
            new(ClaimTypes.Name, name),
        };

        // For Microsoft and Google accounts the "name" is the email
        if (name.Contains('@'))
            claims.Add(new Claim(ClaimTypes.Email, name));

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public static class AppServiceAuthenticationExtensions
{
    public const string AllowedUserPolicy = "AllowedUser";

    /// <summary>
    /// Sign-in and list of allowed emails ("Auth:AllowedEmails"). In development the
    /// API is open ("local" user); outside it, only people on the list get through.
    /// With "Auth:InviteCode" sign-in uses accounts with email and password (see Accounts).
    /// </summary>
    public static IServiceCollection AddAppServiceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var allowed = (configuration.GetSection("Auth:AllowedEmails").Get<string[]>() ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accounts = Accounts.IsEnabled(configuration);

        services.AddAuthentication(accounts ? Accounts.SchemeName : AppServiceAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AppServiceAuthenticationHandler>(AppServiceAuthenticationHandler.SchemeName, null)
            .AddAccountCookie();

        services.AddAccountRateLimit();

        services.AddAuthorizationBuilder()
            .AddPolicy(AllowedUserPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    context.User.Identity?.AuthenticationType == Accounts.SchemeName
                    || (context.User.FindFirstValue(ClaimTypes.Email) is { } email && allowed.Contains(email))));

        return services;
    }

    /// <summary>
    /// Protects the API, except in development (or with "Auth:Disabled": true). With
    /// accounts turned on (invite code), it always protects it.
    /// </summary>
    public static RouteGroupBuilder RequireAllowedUser(this RouteGroupBuilder api, IHostEnvironment environment, IConfiguration configuration)
    {
        if (!Accounts.IsEnabled(configuration)
            && (environment.IsDevelopment() || configuration.GetValue<bool>("Auth:Disabled")))
            return api;

        api.RequireAuthorization(AllowedUserPolicy);
        return api;
    }
}
