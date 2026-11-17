using HolidayApp.Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>"Onde já estive": números, países e abrir a viagem a partir do mapa.</summary>
public sealed class TravelMapTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 11, 15);
    private readonly BunitJSModuleInterop _module;

    public TravelMapTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./js/plan-map.js");
        _module.Mode = JSRuntimeMode.Loose;
    }

    private static VacationSummaryDto Trip(string title, DateOnly start, DateOnly end, string? destination = null, params MapPointDto[] points) =>
        new(Guid.NewGuid(), title, start, end, 0, destination, 0, 0, 0, 0, 0, 0, 0, points);

    private static readonly VacationSummaryDto Roma = Trip("Roma e Paris", new(2026, 8, 3), new(2026, 8, 14), null,
        new MapPointDto("Hotel Roma", 41.9, 12.49), new MapPointDto("Hotel Paris", 48.86, 2.35));
    private static readonly VacationSummaryDto Algarve = Trip("Verão", new(2025, 8, 4), new(2025, 8, 15), "Lagos, Algarve");
    private static readonly VacationSummaryDto Natal = Trip("Natal", new(2026, 12, 24), new(2026, 12, 31), "Porto");

    private IRenderedComponent<TravelMap> RenderMap() =>
        Render<TravelMap>(p => p
            .Add(x => x.Vacations, [Roma, Algarve, Natal])
            .Add(x => x.Today, Today));

    private static Dictionary<string, string> Stats(IRenderedComponent<TravelMap> cut) =>
        cut.FindAll(".travel-stats > div").ToDictionary(d => d.QuerySelector("dt")!.TextContent, d => d.QuerySelector("dd")!.TextContent.Trim());

    [Fact]
    public void Conta_viagens_feitas_dias_e_proximas()
    {
        var cut = RenderMap();

        var stats = Stats(cut);
        Assert.Equal("2", stats["Viagens feitas"]);
        Assert.Equal("24", stats["Dias em viagem"]); // 12 + 12
        Assert.Equal("1", stats["Próximas"]);
    }

    [Fact]
    public void Paises_vem_dos_pontos_e_da_pesquisa_do_destino()
    {
        _module.Setup<List<TravelMap.SearchResult>?>("search", i => (string?)i.Arguments[0] == "Lagos, Algarve")
            .SetResult([new TravelMap.SearchResult { Lat = 37.1, Lng = -8.67, Country = "Portugal" }]);
        _module.Setup<string?>("country", i => (double)i.Arguments[0]! > 45).SetResult("França");
        _module.Setup<string?>("country", i => (double)i.Arguments[0]! < 45).SetResult("Itália");

        var cut = RenderMap();

        cut.WaitForAssertion(() => Assert.Equal("3", Stats(cut)["Países"]), TimeSpan.FromSeconds(10));
        Assert.Equal(["França", "Itália", "Portugal"], cut.FindAll(".travel-countries li").Select(l => l.TextContent));
    }

    [Fact]
    public void Paises_ja_procurados_vem_do_navegador_sem_perguntar_outra_vez()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "holidayapp.geo.v1").SetResult("""
            {"c:41.90,12.49":{"Country":"Itália"},"c:48.86,2.35":{"Country":"França"},
             "q:lagos, algarve":{"Lat":37.1,"Lng":-8.67,"Country":"Portugal"},"q:porto":{"Lat":41.15,"Lng":-8.61,"Country":"Portugal"}}
            """);

        var cut = RenderMap();

        cut.WaitForAssertion(() => Assert.Equal("3", Stats(cut)["Países"]));
        Assert.DoesNotContain(_module.Invocations, i => i.Identifier is "search" or "country");
    }

    [Fact]
    public async Task Carregar_num_ponto_abre_a_viagem()
    {
        var cut = RenderMap();

        await cut.InvokeAsync(() => cut.Instance.OnPlaceClick(0));

        Assert.EndsWith($"/ferias/{Roma.Id}?de=mapa", Services.GetRequiredService<NavigationManager>().Uri);
    }
}
