using System.Net;
using System.Net.Http.Json;
using HolidayApp.Domain.Entities;
using HolidayApp.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Api.Tests;

/// <summary>Accounts: register (with an invite code), sign in, each person's data, delete and export.</summary>
public sealed class AccountTests : IAsyncLifetime
{
    private const string InviteCode = "invite-2026";
    private readonly AppFactory _factory = new("Production", new() { ["Auth:InviteCode"] = InviteCode });

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    /// <summary>A "browser": keeps the session cookie and does not follow redirects (to see them).</summary>
    private HttpClient Browser() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string code = InviteCode,
        string password = "password-one-1", string name = "Ana", string back = "/vacations") =>
        client.PostAsync("register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["name"] = name,
            ["email"] = email,
            ["password"] = password,
            ["inviteCode"] = code,
            ["returnUrl"] = back,
        }));

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string back = "/vacations") =>
        client.PostAsync("login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["returnUrl"] = back,
        }));

    private static Task<HttpResponseMessage> CreateEventAsync(HttpClient client, string title) =>
        client.PostAsJsonAsync("api/events",
            new SaveCalendarEventRequest(title, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 7), EventType.Vacation, null));

    private static async Task<List<string>> TitlesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<CalendarEventDto>>("api/events?year=2026"))!.Select(e => e.Title).ToList();

    [Fact]
    public async Task Without_a_session_the_api_returns_401_and_pages_go_to_login()
    {
        var client = Browser();

        var api = await client.GetAsync("api/settings");
        var page = await client.GetAsync("vacations?from=x");
        var framework = await client.GetAsync("_framework/blazor.web.js");

        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Equal("/login?returnUrl=%2Fvacations%3Ffrom%3Dx", page.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, framework.StatusCode);
    }

    [Fact]
    public async Task Service_worker_opens_without_a_session_so_the_browser_can_update_it()
    {
        var response = await Browser().GetAsync("service-worker.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("holidayapp-v3", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_and_register_pages_open_without_a_session()
    {
        var client = Browser();

        Assert.Contains("Sign in to your account", await client.GetStringAsync("login"));
        Assert.Contains("invite code", await client.GetStringAsync("register"));
    }

    [Fact]
    public async Task Register_without_the_right_code_creates_nothing()
    {
        var response = await RegisterAsync(Browser(), "ana@example.com", code: "wrong");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invite code is wrong", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await _factory.WithDbAsync(db => db.Accounts.CountAsync()));
    }

    [Fact]
    public async Task Register_signs_in_and_returns_to_the_requested_page()
    {
        var client = Browser();

        var response = await RegisterAsync(client, " Ana@Example.COM ");
        var me = await client.GetFromJsonAsync<AccountDto>("api/me");

        Assert.Equal("/vacations", response.Headers.Location!.OriginalString);
        Assert.Equal(new AccountDto("Ana", "ana@example.com", "ana"), me); // email stored in lower case
    }

    [Theory]
    [InlineData("ana@example.com", "short", "at least 8")]
    [InlineData("not-an-email", "password-one-1", "valid email")]
    public async Task Invalid_data_creates_no_account(string email, string password, string message)
    {
        var response = await RegisterAsync(Browser(), email, password: password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task No_two_accounts_with_the_same_email()
    {
        await RegisterAsync(Browser(), "ana@example.com");

        var second = await RegisterAsync(Browser(), "ANA@example.com");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("already an account", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sign_in_with_the_right_password_and_only_with_it()
    {
        await RegisterAsync(Browser(), "ana@example.com", password: "password-one-1");
        var client = Browser();

        var wrong = await LoginAsync(client, "ana@example.com", "something-else");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/settings")).StatusCode);

        var right = await LoginAsync(client, "ANA@example.com", "password-one-1");
        Assert.Equal("/vacations", right.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/settings")).StatusCode);
    }

    [Fact]
    public async Task The_first_account_gets_the_data_that_already_existed()
    {
        // Data from before accounts ("local" user)
        await _factory.WithDbAsync(async db =>
        {
            db.Events.Add(new CalendarEvent { UserId = "local", Title = "Old Algarve", StartDate = new(2026, 8, 3), EndDate = new(2026, 8, 7) });
            return await db.SaveChangesAsync();
        });
        var first = Browser();
        var second = Browser();

        await RegisterAsync(first, "joao@example.com", name: "Joao");
        await RegisterAsync(second, "rui@example.com", name: "Rui");

        Assert.Equal(["Old Algarve"], await TitlesAsync(first));
        Assert.Empty(await TitlesAsync(second));
    }

    [Fact]
    public async Task Each_account_only_sees_its_own_data()
    {
        var ana = Browser();
        var rui = Browser();
        await RegisterAsync(ana, "ana@example.com");
        await RegisterAsync(rui, "rui@example.com", name: "Rui");

        await CreateEventAsync(ana, "Ana's vacation");
        await CreateEventAsync(rui, "Rui's vacation");

        Assert.Equal(["Ana's vacation"], await TitlesAsync(ana));
        Assert.Equal(["Rui's vacation"], await TitlesAsync(rui));
    }

    [Fact]
    public async Task Renaming_updates_the_account_and_the_session()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com");

        var response = await client.PutAsJsonAsync("api/me", new RenameAccountRequest(" Ana Maria "));
        var empty = await client.PutAsJsonAsync("api/me", new RenameAccountRequest(" "));

        Assert.Equal(new AccountDto("Ana Maria", "ana@example.com", "ana"), await response.Content.ReadFromJsonAsync<AccountDto>());
        Assert.Equal("Ana Maria", (await client.GetFromJsonAsync<AccountDto>("api/me"))!.Name);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_asks_for_the_current_one_and_the_new_one_works()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com", password: "password-one-1");

        var wrong = await client.PostAsJsonAsync("api/me/password", new ChangePasswordRequest("wrong", "new-password-2"));
        var tooShort = await client.PostAsJsonAsync("api/me/password", new ChangePasswordRequest("password-one-1", "short"));
        var ok = await client.PostAsJsonAsync("api/me/password", new ChangePasswordRequest("password-one-1", "new-password-2"));

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("current password is wrong", await wrong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await LoginAsync(Browser(), "ana@example.com", "password-one-1")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(Browser(), "ana@example.com", "new-password-2")).StatusCode);
    }

    [Fact]
    public async Task Export_has_the_account_data()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com");
        await CreateEventAsync(client, "Ana's vacation");

        var response = await client.GetAsync("api/me/export");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("holidayapp-data-", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        Assert.Contains("Ana's vacation", json);
        Assert.Contains("ana@example.com", json);
        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_still_works_with_a_broken_plan()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com");
        var created = (await (await CreateEventAsync(client, "Ana's vacation")).Content.ReadFromJsonAsync<CalendarEventDto>())!;
        (await client.PutAsJsonAsync($"api/events/{created.Id}/plan", new VacationPlanDto { Destination = "Rome" })).EnsureSuccessStatusCode();
        await _factory.WithDbAsync(db =>
            db.VacationPlans.IgnoreQueryFilters().ExecuteUpdateAsync(s => s.SetProperty(p => p.Content, "{not json")));

        var response = await client.GetAsync("api/me/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("{not json", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Deleting_the_account_deletes_the_data_and_ends_the_session()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com");
        await CreateEventAsync(client, "Ana's vacation");
        var otherDevice = Browser();
        await LoginAsync(otherDevice, "ana@example.com", "password-one-1");

        var deleted = await client.DeleteAsync("api/me");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("api/me")).StatusCode); // the session on another device too
        Assert.Equal(0, await _factory.WithDbAsync(db => db.Events.IgnoreQueryFilters().CountAsync()));
        Assert.Equal(0, await _factory.WithDbAsync(db => db.Accounts.CountAsync()));
    }

    [Theory]
    [InlineData("//other-site.com")]
    [InlineData("https://other-site.com")]
    [InlineData("/\\other-site.com")]
    public async Task Never_returns_to_another_site(string back)
    {
        var response = await RegisterAsync(Browser(), "ana@example.com", back: back);

        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com");

        await client.PostAsync("logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/settings")).StatusCode);
    }

    [Fact]
    public async Task Opening_the_logout_link_only_asks_it_does_not_sign_out()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com");

        var page = await client.GetAsync("logout");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("""<form method="post" action="/logout">""", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/settings")).StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_signs_out_the_other_devices_but_not_this_one()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com", password: "password-one-1");
        var otherDevice = Browser();
        await LoginAsync(otherDevice, "ana@example.com", "password-one-1");
        Assert.Equal(HttpStatusCode.OK, (await otherDevice.GetAsync("api/settings")).StatusCode);

        (await client.PostAsJsonAsync("api/me/password", new ChangePasswordRequest("password-one-1", "new-password-2"))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("api/settings")).StatusCode);
    }

    [Fact]
    public async Task The_old_GroupPassword_still_works_as_the_invite_code()
    {
        await using var factory = new AppFactory("Production", new() { ["Auth:InviteCode"] = "", ["Auth:GroupPassword"] = "old-code" });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var response = await RegisterAsync(client, "ana@example.com", code: "old-code");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/me")).StatusCode);
    }

    [Fact]
    public async Task Without_an_invite_code_nobody_can_create_an_account_and_the_api_stays_closed()
    {
        await using var factory = new AppFactory("Production", new() { ["Auth:InviteCode"] = "" });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var page = await client.GetStringAsync("register");
        var response = await RegisterAsync(client, "ana@example.com", code: "");

        Assert.Contains("closed", page);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/settings")).StatusCode);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Accounts.CountAsync()));
    }

    [Fact]
    public async Task In_development_the_api_also_needs_an_account()
    {
        await using var factory = new AppFactory();

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("api/settings")).StatusCode);
    }

    [Fact]
    public async Task Without_a_chosen_user_name_one_is_made_from_the_email()
    {
        var first = Browser();
        var second = Browser();
        await RegisterAsync(first, "Ana.Silva+trips@example.com");
        await RegisterAsync(second, "ana.silva@other.example", name: "Other Ana");

        Assert.Equal("ana.silva", (await first.GetFromJsonAsync<AccountDto>("api/me"))!.Username);
        Assert.Equal("ana.silva2", (await second.GetFromJsonAsync<AccountDto>("api/me"))!.Username);
    }

    [Fact]
    public async Task Sign_in_with_the_user_name_too()
    {
        var client = Browser();
        await RegisterAsync(client, "ana@example.com");
        await client.PostAsync("logout", null);

        var response = await LoginAsync(client, "@Ana", "password-one-1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/settings")).StatusCode);
    }

    [Fact]
    public async Task User_name_can_be_changed_but_not_to_one_that_is_taken_or_invalid()
    {
        var ana = Browser();
        var rui = Browser();
        await RegisterAsync(ana, "ana@example.com");
        await RegisterAsync(rui, "rui@example.com", name: "Rui");

        var taken = await ana.PutAsJsonAsync("api/me/username", new ChangeUsernameRequest("RUI"));
        var invalid = await ana.PutAsJsonAsync("api/me/username", new ChangeUsernameRequest("ana silva!"));
        var changed = await ana.PutAsJsonAsync("api/me/username", new ChangeUsernameRequest("@Ana.Travels"));

        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("ana.travels", (await changed.Content.ReadFromJsonAsync<AccountDto>())!.Username);
    }

    [Fact]
    public async Task Accounts_from_before_user_names_get_one_when_the_app_starts()
    {
        await _factory.WithDbAsync(async db =>
        {
            db.Accounts.Add(new UserAccount { Email = "old@example.com", Name = "Old", PasswordHash = "x" });
            return await db.SaveChangesAsync();
        });

        await _factory.WithDbAsync(async db => { await HolidayApp.Infrastructure.Services.AccountService.GiveUsernamesAsync(db); return 0; });

        Assert.Equal("old", await _factory.WithDbAsync(db => db.Accounts.Select(a => a.Username).SingleAsync()));
    }
}
