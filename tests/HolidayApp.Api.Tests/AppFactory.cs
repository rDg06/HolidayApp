using HolidayApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace HolidayApp.Api.Tests;

/// <summary>
/// Starts the real app (API + database) with its own SQLite database,
/// in a temporary file deleted at the end. The API needs an account: use
/// <see cref="SignedInAsync"/> to get a "browser" already signed in.
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly Dictionary<string, string?> _settings;

    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"holidayapp-test-{Guid.NewGuid():N}.db");

    /// <summary>Invite code used unless the test sets another one.</summary>
    public const string DefaultInviteCode = "test-invite";

    public AppFactory(string environment = "Development", Dictionary<string, string?>? settings = null)
    {
        _environment = environment;
        _settings = new() { ["Auth:InviteCode"] = DefaultInviteCode };
        foreach (var (key, value) in settings ?? [])
            _settings[key] = value;
    }

    /// <summary>A "browser": keeps the session cookie and does not follow redirects (to see them).</summary>
    public HttpClient Browser() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Creates an account and returns a "browser" signed in with it.</summary>
    public async Task<HttpClient> SignedInAsync(string email = "ana@example.com", string name = "Ana", string? username = null)
    {
        var client = Browser();
        var response = await client.PostAsync("register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["name"] = name,
            ["email"] = email,
            ["username"] = username ?? "",
            ["password"] = "password-one-1",
            ["inviteCode"] = _settings["Auth:InviteCode"] ?? "",
            ["returnUrl"] = "/",
        }));
        if (response.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException($"Could not create the account {email}: {await response.Content.ReadAsStringAsync()}");
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={DatabasePath}");
        foreach (var (key, value) in _settings)
            builder.UseSetting(key, value);
    }

    /// <summary>Direct database access to check what was saved.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(DatabasePath);
    }
}
