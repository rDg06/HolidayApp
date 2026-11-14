using HolidayApp.Client.Pages;
using HolidayApp.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Client.Tests;

/// <summary>Resumo para partilhar/PDF: por omissão sem valores nem códigos das reservas.</summary>
public sealed class VacationShareTests : BunitContext
{
    private static readonly Guid TripId = Guid.NewGuid();
    private static DateOnly Nov(int day) => new(2026, 11, day);

    public VacationShareTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/plan-map.js").Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICalendarApi>(new FakeApi());
    }

    private IRenderedComponent<VacationShare> RenderShare()
    {
        var cut = Render<VacationShare>(p => p.Add(x => x.Id, TripId));
        cut.WaitForElement(".share-doc");
        return cut;
    }

    [Fact]
    public void Mostra_o_dia_a_dia_com_o_percurso_e_os_outros_locais()
    {
        var cut = RenderShare();

        Assert.Equal(3, cut.FindAll(".share-day").Count);
        var day3 = cut.FindAll(".share-day")[1].TextContent;
        Assert.Contains("Hotel Roma", day3);
        Assert.Contains("Coliseu", day3);
        Assert.Contains("Dia livre", cut.FindAll(".share-day")[2].TextContent);
        Assert.Contains("Metro Colosseo", cut.Find(".share-section:has(h2:contains('Outros locais'))").TextContent);
    }

    [Fact]
    public void Valores_e_codigos_so_aparecem_se_escolhidos()
    {
        var cut = RenderShare();
        var text = cut.Find(".share-doc").TextContent;
        Assert.DoesNotContain("TP832", text);
        Assert.DoesNotContain("189,90", text);
        Assert.Empty(cut.FindAll("h2:contains('Gastos')"));

        cut.Find("input[type=checkbox]:not(:checked)").Change(true); // Valores
        cut.FindAll("input[type=checkbox]").Last().Change(true);     // Códigos e ligações

        text = cut.Find(".share-doc").TextContent;
        Assert.Contains("TP832", text);
        Assert.Contains("189,90 €", text);
        Assert.NotEmpty(cut.FindAll("h2:contains('Gastos')"));
    }

    [Fact]
    public void Guardar_PDF_abre_a_impressao_do_navegador()
    {
        var cut = RenderShare();

        cut.Find("button.primary").Click();

        JSInterop.VerifyInvoke("print");
    }

    private sealed class FakeApi : ICalendarApi
    {
        public Task<CalendarEventDto> GetEventAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(new CalendarEventDto(id, "Roma", Nov(2), Nov(4), EventType.Ferias, null));

        public Task<VacationPlanDto> GetPlanAsync(Guid eventId, CancellationToken ct = default) =>
            Task.FromResult(new VacationPlanDto
            {
                Destination = "Roma",
                Bookings = [new PlanBookingDto { Type = BookingType.Voo, Name = "Lisboa-Roma", Date = Nov(2), Reference = "TP832", Cost = 189.90m }],
                Places =
                [
                    new PlanPlaceDto { Name = "Hotel Roma", Kind = PlaceKind.Alojamento, Date = Nov(2), EndDate = Nov(3), Latitude = 41.9, Longitude = 12.49 },
                    new PlanPlaceDto { Name = "Coliseu", Date = Nov(3), Latitude = 41.89, Longitude = 12.49 },
                    new PlanPlaceDto { Name = "Metro Colosseo", Kind = PlaceKind.Metro, Latitude = 41.891, Longitude = 12.4923 },
                ],
            });

        public Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<YearSummaryDto> GetSummaryAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CalendarEventDto> CreateEventAsync(SaveCalendarEventRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CalendarEventDto> UpdateEventAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteEventAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<VacationPlanDto> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserSettingsDto> SaveSettingsAsync(UserSettingsDto settings, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
