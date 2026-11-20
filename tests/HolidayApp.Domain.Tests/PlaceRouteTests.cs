using HolidayApp.Shared;
using HolidayApp.Shared.Validation;

namespace HolidayApp.Domain.Tests;

public class PlaceRouteTests
{
    private static DateOnly Nov(int day) => new(2026, 11, day);

    [Fact]
    public void Straight_line_distance_between_Lisbon_and_Porto()
    {
        var lisbon = new PlanPlaceDto { Latitude = 38.7223, Longitude = -9.1393 };
        var porto = new PlanPlaceDto { Latitude = 41.1579, Longitude = -8.6291 };

        Assert.InRange(PlaceRoute.DistanceKm(lisbon, porto), 270, 280);
    }

    [Fact]
    public void Route_leaves_the_lodging_follows_the_list_and_ends_at_the_new_lodging_on_moving_day()
    {
        PlanPlaceDto rome = new() { Name = "Hotel Roma", IsLodging = true, Date = Nov(2), EndDate = Nov(4) };
        PlanPlaceDto paris = new() { Name = "Hotel Paris", IsLodging = true, Date = Nov(4), EndDate = Nov(6) };
        PlanPlaceDto colosseum = new() { Name = "Colosseum", Date = Nov(4) };
        PlanPlaceDto other = new() { Name = "Another day", Date = Nov(3) };

        // Paris comes first in the list, but the route starts where you woke up (Rome)
        var stops = PlaceRoute.ForDay([paris, colosseum, other, rome], Nov(4));

        Assert.Equal(["Hotel Roma", "Colosseum", "Hotel Paris"], stops.Select(s => s.Place.Name));
        Assert.Null(stops[0].KmFromPrevious);
        Assert.All(stops.Skip(1), s => Assert.NotNull(s.KmFromPrevious));
    }

    [Fact]
    public void Without_lodging_the_route_is_only_the_days_places()
    {
        PlanPlaceDto a = new() { Name = "A", Date = Nov(3), Latitude = 0, Longitude = 0 };
        PlanPlaceDto b = new() { Name = "B", Date = Nov(3), Latitude = 0, Longitude = 1 };

        var stops = PlaceRoute.ForDay([a, b], Nov(3));

        Assert.Equal(["A", "B"], stops.Select(s => s.Place.Name));
        Assert.InRange(PlaceRoute.TotalKm(stops), 110, 112); // 1 degree at the equator ≈ 111 km
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com")]
    public void Place_link_must_be_http(string url)
    {
        var plan = new VacationPlanDto { Places = [new PlanPlaceDto { Name = "Museum", Url = url }] };

        Assert.True(VacationPlanValidator.Validate(plan).ContainsKey(nameof(plan.Places)));
    }

    [Fact]
    public void Older_plans_with_isLodging_are_still_lodging()
    {
        var json = """{"places":[{"name":"Hotel","isLodging":true},{"name":"Museum"}]}""";
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var plan = System.Text.Json.JsonSerializer.Deserialize<VacationPlanDto>(json, options)!;
        var again = System.Text.Json.JsonSerializer.Serialize(plan, options);

        Assert.Equal([PlaceKind.Lodging, PlaceKind.Visit], plan.Places.Select(p => p.Kind));
        Assert.Contains("\"kind\":\"Metro\"", System.Text.Json.JsonSerializer.Serialize(new PlanPlaceDto { Kind = PlaceKind.Metro }, options));
        Assert.DoesNotContain("isVisit", again);
    }

    [Fact]
    public void Only_visits_are_numbered()
    {
        PlanPlaceDto[] places =
        [
            new() { Name = "Airport", Kind = PlaceKind.Airport },
            new() { Name = "Colosseum" },
            new() { Name = "Metro", Kind = PlaceKind.Metro },
            new() { Name = "Pantheon" },
        ];

        Assert.Equal([null, 1, null, 2], PlanPlaceDto.Numbers(places));
    }

    [Fact]
    public void Invalid_route_mode_is_rejected()
    {
        var plan = new VacationPlanDto { RouteMode = (RouteMode)99 };

        Assert.True(VacationPlanValidator.Validate(plan).ContainsKey(nameof(plan.RouteMode)));
    }
}
