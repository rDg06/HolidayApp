using HolidayApp.Client.Components;
using HolidayApp.Client.Services;

namespace HolidayApp.Client.Tests;

/// <summary>Mapa dos locais: filtro por dia, alojamentos, ordem e percurso (o mapa em si é JavaScript e fica de fora).</summary>
public sealed class PlaceMapTests : BunitContext
{
    private static readonly DateOnly TripStart = new(2026, 11, 2);
    private static readonly DateOnly TripEnd = new(2026, 11, 6);
    private static DateOnly Nov(int day) => new(2026, 11, day);

    public PlaceMapTests()
    {
        // O Leaflet não corre nos testes: as chamadas ao JavaScript devolvem valores vazios
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./js/plan-map.js");
        _module.Mode = JSRuntimeMode.Loose;
        Routing.ClearCache();
    }

    private readonly BunitJSModuleInterop _module;

    private IRenderedComponent<PlaceMap> RenderMap(List<PlanPlaceDto> places, DateOnly? today = null) =>
        Render<PlaceMap>(p => p
            .Add(x => x.Places, places)
            .Add(x => x.TripStart, TripStart)
            .Add(x => x.TripEnd, TripEnd)
            .Add(x => x.Today, today ?? new DateOnly(2026, 1, 1)));

    private static List<string> Names(IRenderedComponent<PlaceMap> cut) =>
        cut.FindAll("input[aria-label='Nome do local']").Select(i => i.GetAttribute("value") ?? "").ToList();

    private static void ClickChip(IRenderedComponent<PlaceMap> cut, string text) =>
        cut.FindAll(".chip").First(c => c.TextContent.Contains(text)).Click();

    /// <summary>Abre o menu do tipo de um local e escolhe uma opção.</summary>
    private static void ChooseKind(IRenderedComponent<PlaceMap> cut, string place, string option)
    {
        cut.Find($"button[aria-label^='Tipo de {place}:']").Click();
        cut.FindAll(".kind-option").First(o => o.TextContent.Contains(option)).Click();
    }

    private static List<PlanPlaceDto> Trip() =>
    [
        new() { Name = "Hotel Roma", IsLodging = true, Date = Nov(2), EndDate = Nov(4), Latitude = 41.9, Longitude = 12.49 },
        new() { Name = "Coliseu", Date = Nov(3), Latitude = 41.8902, Longitude = 12.4922 },
        new() { Name = "Panteão", Date = Nov(3), Latitude = 41.8986, Longitude = 12.4769 },
        new() { Name = "Hotel Paris", IsLodging = true, Date = Nov(4), EndDate = Nov(6), Latitude = 48.86, Longitude = 2.35 },
        new() { Name = "Torre Eiffel", Date = Nov(5), Latitude = 48.8584, Longitude = 2.2945 },
        new() { Name = "Louvre" },
    ];

    [Fact]
    public void Todos_mostra_tudo()
    {
        var cut = RenderMap(Trip());

        Assert.Equal(["Hotel Roma", "Coliseu", "Panteão", "Hotel Paris", "Torre Eiffel", "Louvre"], Names(cut));
    }

    [Fact]
    public void Num_dia_mostra_os_locais_desse_dia_e_o_alojamento_dessa_noite()
    {
        var cut = RenderMap(Trip());

        ClickChip(cut, "ter, 3 nov");
        Assert.Equal(["Hotel Roma", "Coliseu", "Panteão"], Names(cut));

        // Dia da mudança: os dois alojamentos
        ClickChip(cut, "qua, 4 nov");
        Assert.Equal(["Hotel Roma", "Hotel Paris"], Names(cut));

        ClickChip(cut, "Sem dia");
        Assert.Equal(["Louvre"], Names(cut));
    }

    [Fact]
    public void Durante_a_viagem_abre_no_dia_de_hoje()
    {
        var cut = RenderMap(Trip(), today: Nov(5));

        Assert.Equal(["Hotel Paris", "Torre Eiffel"], Names(cut));
        var today = cut.Find(".chip[aria-pressed='true']");
        Assert.Contains("Hoje", today.TextContent);
    }

    [Fact]
    public void Percurso_do_dia_sai_do_alojamento_e_mostra_as_distancias()
    {
        var cut = RenderMap(Trip());

        ClickChip(cut, "ter, 3 nov");

        Assert.Contains("Percurso do dia", cut.Find(".route-total").TextContent);
        var legs = cut.FindAll(".leg").Select(l => l.TextContent).ToList();
        Assert.Equal(2, legs.Count);
        Assert.Contains("desde Hotel Roma", legs[0]);
        Assert.Contains("desde Coliseu", legs[1]);
    }

    [Fact]
    public async Task Arrastar_muda_a_ordem_e_os_numeros()
    {
        var places = Trip();
        var cut = RenderMap(places);
        ClickChip(cut, "ter, 3 nov");

        // Coliseu (posição 1) largado depois do Panteão (posição 2)
        await cut.InvokeAsync(() => cut.Instance.OnReorder(1, 2, after: true));

        Assert.Equal(["Hotel Roma", "Panteão", "Coliseu"], Names(cut));
        Assert.Equal("Panteão", places[1].Name);
        Assert.Equal("1", cut.FindAll(".place-item")[1].QuerySelector(".place-number")!.TextContent.Trim());
    }

    [Fact]
    public async Task Arrastar_para_antes_de_um_local_de_outra_posicao_da_lista()
    {
        var places = Trip();
        var cut = RenderMap(places);

        // Louvre (último) largado antes do Coliseu
        await cut.InvokeAsync(() => cut.Instance.OnReorder(5, 1, after: false));

        Assert.Equal(["Hotel Roma", "Louvre", "Coliseu", "Panteão", "Hotel Paris", "Torre Eiffel"], places.Select(p => p.Name));
    }

    [Fact]
    public async Task Setas_do_teclado_na_pega_trocam_com_o_vizinho_a_vista()
    {
        var places = Trip();
        var cut = RenderMap(places);
        ClickChip(cut, "ter, 3 nov");

        var moved = await cut.InvokeAsync(() => cut.Instance.OnReorderStep(1, +1));
        var stuck = await cut.InvokeAsync(() => cut.Instance.OnReorderStep(moved, +1)); // já é o último do dia

        Assert.Equal(2, moved);
        Assert.Equal(2, stuck);
        Assert.Equal(["Hotel Roma", "Panteão", "Coliseu"], Names(cut));
    }

    [Fact]
    public void So_os_locais_a_visitar_tem_pega_para_arrastar()
    {
        var cut = RenderMap(Trip());

        Assert.Equal(4, cut.FindAll(".drag-handle").Count);
        Assert.Empty(cut.FindAll("button[aria-label^='Subir']"));
    }

    [Fact]
    public void Passar_a_alojamento_fica_do_dia_do_local_ate_ao_fim_da_viagem()
    {
        var places = Trip();
        var cut = RenderMap(places);

        ChooseKind(cut, "Coliseu", "Alojamento");

        var coliseu = places[1];
        Assert.True(coliseu.IsLodging);
        Assert.Equal((Nov(3), TripEnd), (coliseu.Date, coliseu.EndDate));
    }

    [Fact]
    public void Primeiro_dia_depois_do_ultimo_empurra_o_ultimo()
    {
        var places = Trip();
        var cut = RenderMap(places);

        cut.FindAll("select[aria-label='Primeiro dia no alojamento']")[0].Change("2026-11-05");

        Assert.Equal((Nov(5), Nov(5)), (places[0].Date, places[0].EndDate));
    }

    [Fact]
    public async Task Clicar_no_mapa_com_um_dia_escolhido_junta_o_local_nesse_dia()
    {
        var places = Trip();
        var cut = RenderMap(places);
        ClickChip(cut, "sex, 6 nov");

        await cut.InvokeAsync(() => cut.Instance.OnMapClick(48.85, 2.34));

        Assert.Equal(Nov(6), places[^1].Date);
        Assert.Equal("Local 5", places[^1].Name); // os alojamentos não contam para o número
    }

    [Fact]
    public void Notas_e_ligacao_abrem_ao_carregar_no_lapis()
    {
        var places = Trip();
        var cut = RenderMap(places);

        cut.Find("button[aria-label='Notas e ligação de Louvre']").Click();
        cut.Find("textarea[aria-label='Notas de Louvre']").Change("Fecha à terça");
        cut.Find("input[aria-label='Ligação de Louvre']").Change("https://www.louvre.fr");

        Assert.Equal("Fecha à terça", places[5].Notes);
        Assert.Equal("https://www.louvre.fr", cut.Find("a[href='https://www.louvre.fr']").GetAttribute("href"));
    }

    [Fact]
    public void Metro_sem_dia_aparece_em_todos_os_dias_mas_fora_do_percurso()
    {
        var places = Trip();
        places.Add(new() { Name = "Metro Colosseo", Kind = PlaceKind.Metro, Latitude = 41.891, Longitude = 12.4923 });
        var cut = RenderMap(places);

        ClickChip(cut, "ter, 3 nov");

        Assert.Contains("Metro Colosseo", Names(cut));
        Assert.DoesNotContain(cut.FindAll(".leg"), l => l.TextContent.Contains("Metro Colosseo"));
        Assert.Equal(PlaceKind.Metro, places[^1].Kind);
    }

    [Fact]
    public void Aeroporto_com_dia_entra_no_percurso_desse_dia()
    {
        var places = Trip();
        places.Insert(1, new() { Name = "Fiumicino", Kind = PlaceKind.Aeroporto, Date = Nov(3), Latitude = 41.8, Longitude = 12.25 });
        var cut = RenderMap(places);

        ClickChip(cut, "ter, 3 nov");

        var legs = cut.FindAll(".leg").Select(l => l.TextContent).ToList();
        Assert.Contains(legs, l => l.Contains("desde Hotel Roma")); // aeroporto logo a seguir ao hotel
        Assert.Contains(legs, l => l.Contains("desde Fiumicino"));
    }

    [Fact]
    public void Passar_a_autocarro_tira_o_numero_e_o_visitado()
    {
        var places = Trip();
        places[2].Visited = true; // Panteão
        var cut = RenderMap(places);

        ChooseKind(cut, "Panteão", "autocarro");

        var item = cut.FindAll(".place-item")[2];
        Assert.Equal(PlaceKind.Autocarro, places[2].Kind);
        Assert.False(places[2].Visited);
        Assert.Contains("kind-autocarro", item.ClassName);
        Assert.Null(item.QuerySelector("input[type=checkbox]"));
        // Torre Eiffel passa de 3 para 2: os transportes não têm número
        Assert.Equal("2", cut.FindAll(".place-item")[4].QuerySelector(".place-number")!.TextContent.Trim());
    }

    [Fact]
    public void Deixar_de_ser_alojamento_tira_o_ultimo_dia()
    {
        var places = Trip();
        var cut = RenderMap(places);

        ChooseKind(cut, "Hotel Roma", "Estação de comboio");

        Assert.Equal(PlaceKind.Comboio, places[0].Kind);
        Assert.Equal(Nov(2), places[0].Date);
        Assert.Null(places[0].EndDate);
    }

    [Fact]
    public void So_um_menu_de_tipo_aberto_de_cada_vez()
    {
        var cut = RenderMap(Trip());

        cut.Find("button[aria-label^='Tipo de Coliseu:']").Click();
        cut.Find("button[aria-label^='Tipo de Panteão:']").Click();

        var menu = Assert.Single(cut.FindAll(".kind-menu"));
        Assert.Equal("Tipo de Panteão", menu.GetAttribute("aria-label"));

        // Carregar outra vez no mesmo botão fecha o menu
        cut.Find("button[aria-label^='Tipo de Panteão:']").Click();
        Assert.Empty(cut.FindAll(".kind-menu"));
    }

    // ---------- Caminho por ruas ----------

    /// <summary>Caminho falso: Hotel → Coliseu (1,2 km, 15 min) → Panteão (2,1 km, 28 min).</summary>
    private static RouteResult FakeRoute() => new()
    {
        Distance = 3300,
        Duration = 2580,
        Legs = [new RouteLeg { Distance = 1200, Duration = 900 }, new RouteLeg { Distance = 2100, Duration = 1680 }],
        Geometry = [[41.9, 12.49], [41.8902, 12.4922], [41.8986, 12.4769]],
    };

    [Fact]
    public void Num_dia_mostra_o_caminho_por_ruas_com_tempos()
    {
        _module.Setup<RouteResult?>("route", _ => true).SetResult(FakeRoute());
        var cut = RenderMap(Trip());

        ClickChip(cut, "ter, 3 nov");

        cut.WaitForAssertion(() => Assert.Contains("3,3 km · 43 min a pé", cut.Find(".route-total").TextContent));
        var legs = cut.FindAll(".leg").Select(l => l.TextContent).ToList();
        Assert.Contains("1,2 km · 15 min desde Hotel Roma", legs[0]);
        Assert.Contains("2,1 km · 28 min desde Coliseu", legs[1]);
    }

    [Fact]
    public void Sem_servico_de_percursos_fica_a_linha_reta()
    {
        _module.Setup<RouteResult?>("route", _ => true).SetException(new Microsoft.JSInterop.JSException("sem internet"));
        var cut = RenderMap(Trip());

        ClickChip(cut, "ter, 3 nov");

        cut.WaitForAssertion(() => Assert.Contains("sem caminho por ruas", cut.Find(".route-total").TextContent));
        Assert.Contains("em linha reta desde Hotel Roma", cut.FindAll(".leg")[0].TextContent);
    }

    [Fact]
    public void Mudar_para_carro_guarda_o_modo_e_pede_o_caminho_de_carro()
    {
        var route = _module.Setup<RouteResult?>("route", _ => true);
        route.SetResult(FakeRoute());
        RouteMode? chosen = null;
        var cut = Render<PlaceMap>(p => p
            .Add(x => x.Places, Trip())
            .Add(x => x.TripStart, TripStart)
            .Add(x => x.TripEnd, TripEnd)
            .Add(x => x.Today, new DateOnly(2026, 1, 1))
            .Add(x => x.RouteModeChanged, m => chosen = m));
        ClickChip(cut, "ter, 3 nov");

        cut.FindAll(".route-modes .chip").First(c => c.TextContent == "Carro").Click();

        Assert.Equal(RouteMode.Carro, chosen);
        cut.WaitForAssertion(() => Assert.Contains(route.Invocations, i => (string?)i.Arguments[1] == "Carro"));
        Assert.Contains("de carro", cut.Find(".route-total").TextContent);
    }
}
