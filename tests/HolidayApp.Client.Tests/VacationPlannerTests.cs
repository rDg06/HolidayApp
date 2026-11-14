using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>Planeador: separadores por baixo das datas.</summary>
public sealed class VacationPlannerTests : BunitContext
{
    private static readonly Guid TripId = Guid.NewGuid();

    public VacationPlannerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/plan-map.js").Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICalendarApi>(new FakeApi(TripId));
    }

    private IRenderedComponent<VacationPlanner> RenderPlanner()
    {
        var cut = Render<VacationPlanner>(p => p.Add(x => x.Id, TripId));
        cut.WaitForElement(".plan-tabs");
        return cut;
    }

    private static void ClickTab(IRenderedComponent<VacationPlanner> cut, string label) =>
        cut.FindAll("[role=tab]").First(t => t.TextContent.Contains(label)).Click();

    private static string SelectedTab(IRenderedComponent<VacationPlanner> cut) =>
        cut.Find("[role=tab][aria-selected=true]").TextContent;

    [Fact]
    public void Abre_no_separador_Locais_com_o_destino_e_o_mapa()
    {
        var cut = RenderPlanner();

        Assert.Contains("Locais", SelectedTab(cut));
        Assert.NotEmpty(cut.FindAll(".place-map"));
        Assert.Equal("Roma", cut.Find("input[placeholder^='ex.: Algarve']").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".booking-list"));
    }

    [Fact]
    public void Separadores_mostram_os_numeros()
    {
        var cut = RenderPlanner();

        var tabs = cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()).ToList();
        Assert.Equal(5, tabs.Count);
        Assert.Contains(tabs, t => t.StartsWith("Locais") && t.EndsWith("1"));      // 1 visita (hotel e metro não contam)
        Assert.Contains(tabs, t => t.StartsWith("Reservas") && t.EndsWith("2"));
        Assert.Contains(tabs, t => t.StartsWith("Tarefas") && t.EndsWith("1/2"));
    }

    [Fact]
    public void Cada_separador_mostra_so_a_sua_parte()
    {
        var cut = RenderPlanner();

        ClickTab(cut, "Reservas");
        Assert.NotEmpty(cut.FindAll(".booking-list"));
        Assert.Empty(cut.FindAll(".place-map"));

        ClickTab(cut, "Orçamento");
        Assert.Contains("Orçamento", cut.Find(".plan-grid").TextContent);
        Assert.Contains("Onde gastei mais", cut.Find(".plan-grid").TextContent);

        ClickTab(cut, "Dia a dia");
        Assert.Contains("Plano dia a dia", cut.Find(".plan-grid").TextContent);
    }

    [Fact]
    public void Setas_do_teclado_mudam_de_separador()
    {
        var cut = RenderPlanner();

        cut.Find(".plan-tabs").KeyDown(Key.Right);
        Assert.Contains("Reservas", SelectedTab(cut));

        cut.Find(".plan-tabs").KeyDown(Key.Left);
        cut.Find(".plan-tabs").KeyDown(Key.Left);
        Assert.Contains("Dia a dia", SelectedTab(cut)); // dá a volta
    }

    [Fact]
    public void Por_no_mapa_numa_reserva_vai_para_o_separador_Locais()
    {
        var cut = RenderPlanner();
        ClickTab(cut, "Reservas");

        cut.Find(".put-on-map").Click();

        Assert.Contains("Locais", SelectedTab(cut));
        cut.WaitForAssertion(() => Assert.Contains("Hotel Trevi", cut.Find(".lodging-search").TextContent));
    }
}
