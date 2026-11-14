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
            RouteMode = RouteMode.Carro,
        };

        var saved = await (await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", plan)).Content.ReadFromJsonAsync<VacationPlanDto>();
        var reloaded = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");

        Assert.Equal("Algarve", saved!.Destination);
        Assert.Equal("Algarve", reloaded!.Destination);
        Assert.Equal("Faro", Assert.Single(reloaded.Days).Place);
        Assert.Equal("Malas", Assert.Single(reloaded.Tasks).Text);
        Assert.Equal(RouteMode.Carro, reloaded.RouteMode);
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
    public async Task Gravar_por_cima_de_outra_pessoa_e_recusado()
    {
        var ev = await CreateVacationAsync();

        // Duas pessoas abrem a mesma viagem (ainda sem planeamento: versão 0)
        var ana = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");
        var rui = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");
        Assert.Equal(0, ana!.Version);

        ana.Destination = "Algarve";
        var anaSaves = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", ana);
        rui!.Destination = "Açores";
        var ruiSaves = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", rui);

        Assert.Equal(HttpStatusCode.OK, anaSaves.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ruiSaves.StatusCode);
        Assert.Contains("Outra pessoa", await ruiSaves.Content.ReadAsStringAsync());
        Assert.Equal("Algarve", (await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan"))!.Destination);
    }

    [Fact]
    public async Task Gravar_com_a_versao_devolvida_continua_a_funcionar()
    {
        var ev = await CreateVacationAsync();
        var plan = (await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan"))!;

        plan.Destination = "Algarve";
        var first = (await (await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", plan)).Content.ReadFromJsonAsync<VacationPlanDto>())!;
        first.Destination = "Lagos";
        var second = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", first);

        Assert.NotEqual(0, first.Version);
        Assert.True(first.Version < 9_007_199_254_740_991); // cabe num número do JavaScript sem arredondar
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
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
    public async Task Alojamentos_guardam_a_estadia_e_nao_contam_como_locais_a_visitar()
    {
        var ev = await CreateVacationAsync();
        await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto
        {
            Places =
            [
                new PlanPlaceDto { Name = "Hotel Faro", IsLodging = true, Date = new DateOnly(2026, 8, 3), EndDate = new DateOnly(2026, 8, 8), Visited = true },
                new PlanPlaceDto { Name = "Casa Lagos", IsLodging = true, Date = new DateOnly(2026, 8, 8) }, // sem fim: um só dia
                new PlanPlaceDto { Name = "Praia", Date = new DateOnly(2026, 8, 4), EndDate = new DateOnly(2026, 8, 6), Visited = true },
                new PlanPlaceDto { Name = "Farol" },
            ],
        });

        var plan = await _client.GetFromJsonAsync<VacationPlanDto>($"api/events/{ev.Id}/plan");
        var summary = Assert.Single((await _client.GetFromJsonAsync<List<VacationSummaryDto>>("api/vacations"))!);

        var (faro, lagos, praia) = (plan!.Places[0], plan.Places[1], plan.Places[2]);
        Assert.Equal((new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 8)), (faro.Date, faro.EndDate));
        Assert.False(faro.Visited);
        Assert.Equal(new DateOnly(2026, 8, 8), lagos.EndDate);
        Assert.Null(praia.EndDate); // só o alojamento tem fim
        Assert.True(faro.StaysOn(new DateOnly(2026, 8, 8)) && lagos.StaysOn(new DateOnly(2026, 8, 8))); // dia da mudança
        Assert.Equal((2, 1), (summary.Places, summary.PlacesVisited));
    }

    [Fact]
    public async Task Alojamento_com_saida_antes_da_entrada_ou_fora_das_ferias_e_recusado()
    {
        var ev = await CreateVacationAsync();

        var backwards = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto
        {
            Places = [new PlanPlaceDto { Name = "Hotel", IsLodging = true, Date = new DateOnly(2026, 8, 6), EndDate = new DateOnly(2026, 8, 4) }],
        });
        var outside = await _client.PutAsJsonAsync($"api/events/{ev.Id}/plan", new VacationPlanDto
        {
            Places = [new PlanPlaceDto { Name = "Hotel", IsLodging = true, Date = new DateOnly(2026, 8, 6), EndDate = new DateOnly(2026, 8, 20) }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
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
