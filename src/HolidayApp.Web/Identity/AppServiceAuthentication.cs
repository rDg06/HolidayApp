using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HolidayApp.Web.Identity;

/// <summary>
/// Lê o utilizador autenticado pela "Authentication" do Azure App Service (Easy Auth).
/// O App Service faz o login (Microsoft, Google…) antes de o pedido chegar à app e
/// envia quem é nos cabeçalhos X-MS-CLIENT-PRINCIPAL-*. Esses cabeçalhos só são de
/// confiança quando o App Service está mesmo à frente (WEBSITE_AUTH_ENABLED=True),
/// porque é ele que apaga os que viessem do navegador.
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
            // O fornecedor faz parte do id: o mesmo id em fornecedores diferentes são pessoas diferentes
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(provider) ? id : $"{provider}:{id}"),
            new(ClaimTypes.Name, name),
        };

        // Para contas Microsoft e Google o "name" é o email
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
    /// Login e lista de emails autorizados ("Auth:AllowedEmails"). Em desenvolvimento a
    /// API fica aberta (utilizador "local"); fora dele, só passa quem estiver na lista.
    /// </summary>
    public static IServiceCollection AddAppServiceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var allowed = (configuration.GetSection("Auth:AllowedEmails").Get<string[]>() ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        services.AddAuthentication(AppServiceAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AppServiceAuthenticationHandler>(AppServiceAuthenticationHandler.SchemeName, null);

        services.AddAuthorizationBuilder()
            .AddPolicy(AllowedUserPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    context.User.FindFirstValue(ClaimTypes.Email) is { } email && allowed.Contains(email)));

        return services;
    }

    /// <summary>Protege a API, exceto em desenvolvimento (ou com "Auth:Disabled": true).</summary>
    public static RouteGroupBuilder RequireAllowedUser(this RouteGroupBuilder api, IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment() || configuration.GetValue<bool>("Auth:Disabled"))
            return api;

        api.RequireAuthorization(AllowedUserPolicy);
        return api;
    }
}
