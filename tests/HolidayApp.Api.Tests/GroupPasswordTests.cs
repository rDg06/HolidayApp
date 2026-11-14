using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HolidayApp.Api.Tests;

/// <summary>Palavra-passe do grupo: sem sessão só a página de entrada abre.</summary>
public sealed class GroupPasswordTests : IAsyncLifetime
{
    private const string Password = "praia-2026";
    private readonly AppFactory _factory = new("Production", new()
    {
        ["Auth:GroupPassword"] = Password,
        ["Auth:Disabled"] = "true", // a palavra-passe ganha: mesmo assim tem de pedir login
    });
    private HttpClient _client = default!;

    public Task InitializeAsync()
    {
        // Guarda o cookie da sessão, mas não segue os redirecionamentos (para os ver)
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private Task<HttpResponseMessage> LoginAsync(string password, string back = "/ferias") =>
        _client.PostAsync("entrar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["senha"] = password,
            ["volta"] = back,
        }));

    [Fact]
    public async Task Sem_sessao_a_api_da_401_e_as_paginas_mandam_para_a_entrada()
    {
        var api = await _client.GetAsync("api/settings");
        var page = await _client.GetAsync("ferias?de=x");
        var framework = await _client.GetAsync("_framework/blazor.web.js");

        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/entrar?volta=%2Fferias%3Fde%3Dx", page.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, framework.StatusCode); // nem os ficheiros da app
    }

    [Fact]
    public async Task Pagina_de_entrada_abre_sem_sessao()
    {
        var response = await _client.GetAsync("entrar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("palavra-passe do grupo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Palavra_passe_errada_nao_entra()
    {
        var login = await LoginAsync("errada");
        var api = await _client.GetAsync("api/settings");

        Assert.StartsWith("/entrar?erro=true", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task Palavra_passe_certa_entra_e_volta_a_pagina_pedida()
    {
        var login = await LoginAsync(Password);
        var api = await _client.GetAsync("api/settings");

        Assert.Equal("/ferias", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);
    }

    [Theory]
    [InlineData("//outro-site.com")]
    [InlineData("https://outro-site.com")]
    [InlineData("/\\outro-site.com")]
    public async Task Nunca_volta_para_outro_site(string back)
    {
        var login = await LoginAsync(Password, back);

        Assert.Equal("/", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Sair_fecha_a_sessao()
    {
        await LoginAsync(Password);

        await _client.GetAsync("sair");
        var api = await _client.GetAsync("api/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }
}
