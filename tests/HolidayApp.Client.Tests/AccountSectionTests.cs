using HolidayApp.Client.Components;
using HolidayApp.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>The profile section on the "Profile and settings" page.</summary>
public sealed class AccountSectionTests : BunitContext
{
    private readonly FakeApi _api = new(Guid.NewGuid());

    public AccountSectionTests() => Services.AddSingleton<ICalendarApi>(_api);

    [Fact]
    public void Not_shown_without_accounts()
    {
        var cut = Render<AccountSection>();

        Assert.Empty(cut.FindAll(".account"));
    }

    [Fact]
    public void Shows_who_is_signed_in_and_the_link_to_download_the_data()
    {
        _api.Me = new AccountDto("Ana", "ana@example.com");

        var cut = Render<AccountSection>();

        Assert.Equal("Ana", cut.Find("input[aria-label='Name']").GetAttribute("value"));
        Assert.Contains("ana@example.com", cut.Find(".account-email").TextContent);
        Assert.True(cut.Find("a[href='api/me/export']").HasAttribute("download"));
    }

    [Fact]
    public void Delete_only_after_typing_the_email()
    {
        _api.Me = new AccountDto("Ana", "ana@example.com");
        var cut = Render<AccountSection>();

        cut.FindAll("button").First(b => b.TextContent == "Delete my account").Click();
        var confirm = cut.FindAll("button").First(b => b.TextContent.Contains("Delete everything"));
        Assert.True(confirm.HasAttribute("disabled"));

        cut.Find(".account-delete input").Input("ANA@example.com ");
        cut.FindAll("button").First(b => b.TextContent.Contains("Delete everything")).Click();

        Assert.True(_api.MeDeleted);
        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Change_the_name()
    {
        _api.Me = new AccountDto("Ana", "ana@example.com");
        var cut = Render<AccountSection>();
        var save = () => cut.FindAll("button").First(b => b.TextContent.Contains("Save name"));
        Assert.True(save().HasAttribute("disabled")); // nothing changed

        cut.Find("input[aria-label='Name']").Input(" Ana Maria ");
        cut.Find(".account-name").Submit();

        Assert.Equal("Ana Maria", _api.Me!.Name);
        Assert.Contains("Saved", cut.Find(".account-name").TextContent);
    }

    [Fact]
    public void Changing_the_password_asks_for_the_current_one_and_the_new_one_twice()
    {
        _api.Me = new AccountDto("Ana", "ana@example.com");
        var cut = Render<AccountSection>();
        cut.FindAll("button").First(b => b.TextContent == "Change password").Click();

        // The two new ones are not the same
        cut.Find("input[aria-label='Current password']").Change("right-current");
        cut.Find("input[aria-label='New password']").Change("new-password-1");
        cut.Find("input[aria-label='Repeat the new password']").Change("new-password-2");
        cut.Find(".account-password").Submit();
        Assert.Contains("not the same", cut.Find(".account-password .form-errors").TextContent);

        // Wrong current one: the server error shows
        cut.Find("input[aria-label='Current password']").Change("wrong");
        cut.Find("input[aria-label='Repeat the new password']").Change("new-password-1");
        cut.Find(".account-password").Submit();
        Assert.Contains("current password is wrong", cut.Find(".account-password .form-errors").TextContent);

        // All good
        cut.Find("input[aria-label='Current password']").Change("right-current");
        cut.Find(".account-password").Submit();
        Assert.Equal("new-password-1", _api.ChangedPassword);
        Assert.Contains("Password changed", cut.Find(".account").TextContent);
        Assert.Empty(cut.FindAll(".account-password"));
    }

    [Fact]
    public void Sign_out_posts_to_the_servers_logout_page()
    {
        _api.Me = new AccountDto("Ana", "ana@example.com");
        var cut = Render<AccountSection>();

        var button = cut.FindAll("button").First(b => b.TextContent == "Sign out");
        var form = button.Closest("form")!;

        Assert.Equal("submit", button.GetAttribute("type"));
        Assert.Equal(("post", "logout"), (form.GetAttribute("method"), form.GetAttribute("action")));
    }
}
