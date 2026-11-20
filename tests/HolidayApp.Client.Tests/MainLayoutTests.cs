using HolidayApp.Client.Layout;

namespace HolidayApp.Client.Tests;

/// <summary>Header: the sign-out button.</summary>
public sealed class MainLayoutTests : BunitContext
{
    [Fact]
    public void Sign_out_button_posts_to_the_servers_logout_page()
    {
        var cut = Render<MainLayout>();

        var button = cut.Find("button[aria-label='Sign out']");
        var form = button.Closest("form")!;

        Assert.Equal("submit", button.GetAttribute("type"));
        Assert.Equal(("post", "logout"), (form.GetAttribute("method"), form.GetAttribute("action")));
    }
}
