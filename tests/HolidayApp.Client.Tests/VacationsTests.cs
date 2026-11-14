using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>Página "Férias": números de cada viagem.</summary>
public sealed class VacationsTests : BunitContext
{
    public VacationsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICalendarApi>(new FakeApi(Guid.NewGuid()));
    }

    [Fact]
    public void Cartao_mostra_o_total_de_dias_e_os_locais_a_visitar()
    {
        var cut = Render<Vacations>();
        cut.WaitForElement(".vacation-stats");

        var stats = cut.FindAll(".vacation-stats > div")
            .ToDictionary(d => d.QuerySelector("dt")!.TextContent.Trim(), d => d.QuerySelector("dd")!.TextContent.Trim());

        Assert.StartsWith("5", stats["Dias"]);
        Assert.Contains("4 úteis", stats["Dias"]);
        Assert.StartsWith("3", stats["Locais a visitar"]);
        Assert.Contains("1 visitado", stats["Locais a visitar"]);
    }
}
