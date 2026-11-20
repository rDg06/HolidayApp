using HolidayApp.Client.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>Header: the sign-out button.</summary>
public sealed class MainLayoutTests : BunitContext
{
    [Fact]
    public void Sign_out_button_goes_to_the_servers_logout_page()
    {
        var cut = Render<MainLayout>();

        cut.Find("button[aria-label='Sign out']").Click();

        Assert.EndsWith("/logout", Services.GetRequiredService<NavigationManager>().Uri);
    }
}
