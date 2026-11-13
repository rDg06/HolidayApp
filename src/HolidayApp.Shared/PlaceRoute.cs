namespace HolidayApp.Shared;

/// <summary>Uma paragem do percurso de um dia, com a distância desde a paragem anterior.</summary>
public sealed record RouteStop(PlanPlaceDto Place, double? KmFromPrevious);

/// <summary>
/// Percurso de um dia: sai do alojamento onde se acordou, passa pelos locais desse dia
/// pela ordem da lista e acaba no alojamento onde se vai dormir (se for outro, no dia
/// da mudança). As distâncias são em linha reta.
/// </summary>
public static class PlaceRoute
{
    public static List<RouteStop> ForDay(IReadOnlyList<PlanPlaceDto> places, DateOnly day)
    {
        var lodgings = places.Where(p => p.StaysOn(day)).OrderBy(p => p.Date).ToList();
        var visits = places.Where(p => !p.IsLodging && p.Date == day);

        var path = new List<PlanPlaceDto>();
        if (lodgings.Count > 0)
            path.Add(lodgings[0]);
        path.AddRange(visits);
        if (lodgings.Count > 1)
            path.Add(lodgings[^1]);

        var stops = new List<RouteStop>(path.Count);
        for (var i = 0; i < path.Count; i++)
            stops.Add(new RouteStop(path[i], i == 0 ? null : DistanceKm(path[i - 1], path[i])));
        return stops;
    }

    /// <summary>Distância total do percurso (km, em linha reta).</summary>
    public static double TotalKm(IEnumerable<RouteStop> stops) => stops.Sum(s => s.KmFromPrevious ?? 0);

    /// <summary>Distância em linha reta entre dois locais (fórmula de Haversine), em km.</summary>
    public static double DistanceKm(PlanPlaceDto a, PlanPlaceDto b)
    {
        const double EarthRadiusKm = 6371.0;
        static double Rad(double degrees) => degrees * Math.PI / 180;

        var dLat = Rad(b.Latitude - a.Latitude);
        var dLng = Rad(b.Longitude - a.Longitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(a.Latitude)) * Math.Cos(Rad(b.Latitude)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }
}
