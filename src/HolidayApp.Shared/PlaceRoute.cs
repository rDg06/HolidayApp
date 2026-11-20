namespace HolidayApp.Shared;

/// <summary>A stop on a day's route, with the distance from the previous stop.</summary>
public sealed record RouteStop(PlanPlaceDto Place, double? KmFromPrevious);

/// <summary>
/// A day's route: leaves the lodging you woke up in, goes through that day's places
/// (visits and transport with that day, in list order) and ends at the lodging where you
/// sleep (if it is a different one, on the day you move). Transport without a day is left out.
/// Distances are straight-line.
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

    /// <summary>Total route distance (km, straight-line).</summary>
    public static double TotalKm(IEnumerable<RouteStop> stops) => stops.Sum(s => s.KmFromPrevious ?? 0);

    /// <summary>Straight-line distance between two places (Haversine formula), in km.</summary>
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
