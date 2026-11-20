using System.Net;

namespace HolidayApp.Api.Tests;

/// <summary>Outside development, the API only accepts people signed in through App Service and on the list.</summary>
public sealed class AuthTests
{
    private static AppFactory Published(bool appServiceAuth) => new("Production", new()
    {
        ["WEBSITE_AUTH_ENABLED"] = appServiceAuth ? "True" : null,
        ["Auth:AllowedEmails:0"] = "me@example.com",
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
    public async Task In_development_the_api_is_open()
    {
        await using var factory = new AppFactory();

        var response = await factory.CreateClient().SendAsync(Request(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Published_without_sign_in_returns_401()
    {
        await using var factory = Published(appServiceAuth: true);

        var response = await factory.CreateClient().SendAsync(Request(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Published_with_an_allowed_email_gets_in()
    {
        await using var factory = Published(appServiceAuth: true);

        var response = await factory.CreateClient().SendAsync(Request("ME@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Published_with_another_email_returns_403()
    {
        await using var factory = Published(appServiceAuth: true);

        var response = await factory.CreateClient().SendAsync(Request("someone.else@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Fake_headers_are_ignored_without_App_Service()
    {
        // Without WEBSITE_AUTH_ENABLED, nothing guarantees the headers come from App Service
        await using var factory = Published(appServiceAuth: false);

        var response = await factory.CreateClient().SendAsync(Request("me@example.com"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
