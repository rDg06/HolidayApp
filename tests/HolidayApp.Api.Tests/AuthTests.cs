using System.Net;

namespace HolidayApp.Api.Tests;

/// <summary>Fora de desenvolvimento, a API só aceita quem fez login no App Service e está na lista.</summary>
public sealed class AuthTests
{
    private static AppFactory Published(bool appServiceAuth) => new("Production", new()
    {
        ["WEBSITE_AUTH_ENABLED"] = appServiceAuth ? "True" : null,
        ["Auth:AllowedEmails:0"] = "eu@exemplo.pt",
    });

    private static HttpRequestMessage Request(string? email)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/settings");
        if (email is not null)
        {
            request.Headers.Add("X-MS-CLIENT-PRINCIPAL-ID", "abc123");
            request.Headers.Add("X-MS-CLIENT-PRINCIPAL-IDP", "aad");
            request.Headers.Add("X-MS-CLIENT-PRINCIPAL-NAME", email);
        }
        return request;
    }

    [Fact]
    public async Task Em_desenvolvimento_a_api_esta_aberta()
    {
        await using var factory = new AppFactory();

        var response = await factory.CreateClient().SendAsync(Request(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Publicada_sem_login_da_401()
    {
        await using var factory = Published(appServiceAuth: true);

        var response = await factory.CreateClient().SendAsync(Request(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Publicada_com_email_autorizado_entra()
    {
        await using var factory = Published(appServiceAuth: true);

        var response = await factory.CreateClient().SendAsync(Request("EU@exemplo.pt"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Publicada_com_outro_email_da_403()
    {
        await using var factory = Published(appServiceAuth: true);

        var response = await factory.CreateClient().SendAsync(Request("outra.pessoa@exemplo.pt"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cabecalhos_falsos_sao_ignorados_sem_o_App_Service()
    {
        // Sem WEBSITE_AUTH_ENABLED, ninguém garante que os cabeçalhos vêm do App Service
        await using var factory = Published(appServiceAuth: false);

        var response = await factory.CreateClient().SendAsync(Request("eu@exemplo.pt"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
