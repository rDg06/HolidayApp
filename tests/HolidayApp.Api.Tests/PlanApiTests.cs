using System.Net;
using System.Net.Http.Json;
using HolidayApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Api.Tests;

public sealed class PlanApiTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private HttpClient _client = default!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<CalendarEventDto> CreateVacationAsync(string start = "2026-08-03", string end = "2026-08-14")
    {
        var response = await _client.PostAsJsonAsync("api/events",
            new SaveCalendarEventRequest("Algarve", DateOnly.Parse(start), DateOnly.Parse(end), EventType.Ferias, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CalendarEventDto>())!;
    }

    [Fact]
    public async Task Guarda_e_devolve_o_planeamento_limpo()
    {
        var ev = await CreateVacationAsync();
        var plan = new VacationPlanDto
        {
            Destination = "  Algarve  ",
            Days =
            [
                new PlanDayDto { Date = new DateOnly(2026, 8, 3), Place = " Faro " },
                new PlanDayDto { Date = new DateOnly(2026, 8, 4) }, // vazio: não é guardado
            ],
            Tasks = [new PlanTaskDto { Text = " Malas " }],
        };

        var saved = await (await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", plan)).Content.ReadFromJsonAsync<VacationPlanDto>();
        var reloaded = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");

        Assert.Equal("Algarve", saved!.Destination);
        Assert.Equal("Algarve", reloaded!.Destination);
        Assert.Equal("Faro", Assert.Single(reloaded.Days).Place);
        Assert.Equal("Malas", Assert.Single(reloaded.Tasks).Text);
    }

    [Theory]
    [InlineData("""{"bookings":[null]}""")]
    [InlineData("""{"tasks":[null],"places":[null]}""")]
    public async Task Elementos_vazios_dao_erro_400_e_nao_500(string body)
    {
        var ev = await CreateVacationAsync();

        var response = await _client.PutAsync($"api/events/{ev.Id}/plan",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("9999-12-31")]
    [InlineData("0001-01-01")]
    public async Task Marcacoes_fora_dos_anos_suportados_sao_recusadas(string date)
    {
        var response = await _client.PostAsJsonAsync("api/events",
            new SaveCalendarEventRequest("x", DateOnly.Parse(date), DateOnly.Parse(date), EventType.Ferias, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reserva_fora_das_datas_das_ferias_e_recusada()
    {
        var ev = await CreateVacationAsync();
        var plan = new VacationPlanDto { Bookings = [new PlanBookingDto { Name = "Hotel", Date = new DateOnly(2026, 9, 1) }] };

        var response = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", plan);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Planeamento_de_marcacao_inexistente_da_404()
    {
        var response = await _client.PutAsJsonAsync($"api/events/{Guid.NewGuid()}/plan", new VacationPlanDto());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Duas_gravacoes_em_simultaneo_nao_falham()
    {
        var ev = await CreateVacationAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto { Destination = $"Destino {i}" })));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await _factory.WithDbAsync(db => db.VacationPlans.IgnoreQueryFilters().CountAsync()));
    }

    [Fact]
    public async Task Apagar_a_marcacao_apaga_o_planeamento()
    {
        var ev = await CreateVacationAsync();
        await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto { Destination = "Algarve" });

        (await _client.DeleteAsync($"api/events/{ev.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(0, await _factory.WithDbAsync(db => db.VacationPlans.IgnoreQueryFilters().CountAsync()));
    }

    [Fact]
    public async Task Resumo_das_ferias_conta_dias_uteis_e_gastos()
    {
        // 3 a 14 de agosto de 2026: 10 dias de semana, nenhum feriado
        var ev = await CreateVacationAsync();
        await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto
        {
            Bookings = [new PlanBookingDto { Type = BookingType.Alojamento, Name = "Hotel", Cost = 500 }],
            Expenses = [new PlanExpenseDto { Category = ExpenseCategory.Alimentacao, Planned = 200, Actual = 250 }],
            Tasks = [new PlanTaskDto { Text = "Malas", Done = true }, new PlanTaskDto { Text = "Bilhetes" }],
        });

        var summary = Assert.Single((await _client.GetFromJsonAsync<List<VacationSummaryDto>>("api/vacations"))!);

        Assert.Equal(10, summary.Workdays);
        Assert.Equal(700m, summary.Planned);
        Assert.Equal(750m, summary.Spent);
        Assert.Equal((2, 1), (summary.Tasks, summary.TasksDone));
    }

    [Fact]
    public async Task Feriado_nao_conta_como_dia_util()
    {
        // 11 a 15 de agosto de 2025: segunda a sexta, mas 15/08 (Assunção) é feriado → 4 dias úteis
        await CreateVacationAsync("2025-08-11", "2025-08-15");

        var summary = Assert.Single((await _client.GetFromJsonAsync<List<VacationSummaryDto>>("api/vacations"))!);

        Assert.Equal(4, summary.Workdays);
    }

    [Fact]
    public async Task Definicoes_gravadas_em_simultaneo_nao_falham()
    {
        var settings = new UserSettingsDto { AnnualVacationDays = 25 };

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _client.PutAsJsonAsync("api/settings", settings)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(25, (await _client.GetFromJsonAsync<UserSettingsDto>("api/settings"))!.AnnualVacationDays);
    }
}
