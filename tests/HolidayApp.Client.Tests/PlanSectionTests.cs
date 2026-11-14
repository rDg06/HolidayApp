using HolidayApp.Client.Components;

namespace HolidayApp.Client.Tests;

public sealed class PlanSectionTests : BunitContext
{
    public PlanSectionTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Carregar_no_titulo_recolhe_e_volta_a_mostrar()
    {
        var cut = Render<PlanBookings>(p => p
            .Add(x => x.Bookings, [new PlanBookingDto { Name = "Hotel" }])
            .Add(x => x.TripStart, new DateOnly(2026, 11, 2))
            .Add(x => x.TripEnd, new DateOnly(2026, 11, 6)));

        var toggle = cut.Find(".section-toggle");
        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        Assert.NotEmpty(cut.FindAll("button:contains('+ Reserva')"));

        toggle.Click();

        Assert.Equal("false", cut.Find(".section-toggle").GetAttribute("aria-expanded"));
        Assert.True(cut.Find(".plan-section-body").HasAttribute("hidden"));
        Assert.Empty(cut.FindAll("button:contains('+ Reserva')")); // botões só com a secção aberta
        Assert.Contains("1 reserva", cut.Find(".section-extra").TextContent); // o resumo fica à vista

        cut.Find(".section-toggle").Click();
        Assert.False(cut.Find(".plan-section-body").HasAttribute("hidden"));
    }

    [Fact]
    public void Guarda_as_seccoes_recolhidas_no_navegador()
    {
        var cut = Render<PlanTasks>(p => p.Add(x => x.Tasks, []));

        cut.Find(".section-toggle").Click();

        var saved = JSInterop.VerifyInvoke("localStorage.setItem");
        Assert.Equal("tasks", saved.Arguments[1]);
    }

    [Fact]
    public void Abre_recolhida_se_ficou_assim_da_ultima_vez()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "holidayapp.planner.collapsed").SetResult("tasks,budget");

        var cut = Render<PlanTasks>(p => p.Add(x => x.Tasks, []));

        cut.WaitForAssertion(() => Assert.True(cut.Find(".plan-section-body").HasAttribute("hidden")));
    }
}
