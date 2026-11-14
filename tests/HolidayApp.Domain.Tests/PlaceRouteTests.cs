using HolidayApp.Shared;
using HolidayApp.Shared.Validation;

namespace HolidayApp.Domain.Tests;

public class PlaceRouteTests
{
    private static DateOnly Nov(int day) => new(2026, 11, day);

    [Fact]
    public void Distancia_em_linha_reta_entre_Lisboa_e_Porto()
    {
        var lisboa = new PlanPlaceDto { Latitude = 38.7223, Longitude = -9.1393 };
        var porto = new PlanPlaceDto { Latitude = 41.1579, Longitude = -8.6291 };

        Assert.InRange(PlaceRoute.DistanceKm(lisboa, porto), 270, 280);
    }

    [Fact]
    public void Percurso_sai_do_alojamento_segue_a_lista_e_acaba_no_novo_alojamento_no_dia_da_mudanca()
    {
        PlanPlaceDto roma = new() { Name = "Hotel Roma", IsLodging = true, Date = Nov(2), EndDate = Nov(4) };
        PlanPlaceDto paris = new() { Name = "Hotel Paris", IsLodging = true, Date = Nov(4), EndDate = Nov(6) };
        PlanPlaceDto coliseu = new() { Name = "Coliseu", Date = Nov(4) };
        PlanPlaceDto outro = new() { Name = "Outro dia", Date = Nov(3) };

        // Paris aparece antes na lista, mas o percurso começa onde se acordou (Roma)
        var stops = PlaceRoute.ForDay([paris, coliseu, outro, roma], Nov(4));

        Assert.Equal(["Hotel Roma", "Coliseu", "Hotel Paris"], stops.Select(s => s.Place.Name));
        Assert.Null(stops[0].KmFromPrevious);
        Assert.All(stops.Skip(1), s => Assert.NotNull(s.KmFromPrevious));
    }

    [Fact]
    public void Sem_alojamento_o_percurso_e_so_os_locais_do_dia()
    {
        PlanPlaceDto a = new() { Name = "A", Date = Nov(3), Latitude = 0, Longitude = 0 };
        PlanPlaceDto b = new() { Name = "B", Date = Nov(3), Latitude = 0, Longitude = 1 };

        var stops = PlaceRoute.ForDay([a, b], Nov(3));

        Assert.Equal(["A", "B"], stops.Select(s => s.Place.Name));
        Assert.InRange(PlaceRoute.TotalKm(stops), 110, 112); // 1 grau no equador ≈ 111 km
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://exemplo.pt")]
    public void Ligacao_do_local_tem_de_ser_http(string url)
    {
        var plan = new VacationPlanDto { Places = [new PlanPlaceDto { Name = "Museu", Url = url }] };

        Assert.True(VacationPlanValidator.Validate(plan).ContainsKey(nameof(plan.Places)));
    }

    [Fact]
    public void Planeamentos_antigos_com_isLodging_continuam_a_ser_alojamento()
    {
        var json = """{"places":[{"name":"Hotel","isLodging":true},{"name":"Museu"}]}""";
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var plan = System.Text.Json.JsonSerializer.Deserialize<VacationPlanDto>(json, options)!;
        var again = System.Text.Json.JsonSerializer.Serialize(plan, options);

        Assert.Equal([PlaceKind.Alojamento, PlaceKind.Visita], plan.Places.Select(p => p.Kind));
        Assert.Contains("\"kind\":\"Metro\"", System.Text.Json.JsonSerializer.Serialize(new PlanPlaceDto { Kind = PlaceKind.Metro }, options));
        Assert.DoesNotContain("isVisit", again);
    }

    [Fact]
    public void So_as_visitas_sao_numeradas()
    {
        PlanPlaceDto[] places =
        [
            new() { Name = "Aeroporto", Kind = PlaceKind.Aeroporto },
            new() { Name = "Coliseu" },
            new() { Name = "Metro", Kind = PlaceKind.Metro },
            new() { Name = "Panteão" },
        ];

        Assert.Equal([null, 1, null, 2], PlanPlaceDto.Numbers(places));
    }

    [Fact]
    public void Modo_de_percurso_invalido_e_recusado()
    {
        var plan = new VacationPlanDto { RouteMode = (RouteMode)99 };

        Assert.True(VacationPlanValidator.Validate(plan).ContainsKey(nameof(plan.RouteMode)));
    }
}
