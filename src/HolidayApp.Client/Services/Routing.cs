using HolidayApp.Shared;
using Microsoft.JSInterop;

namespace HolidayApp.Client.Services;

/// <summary>A leg of the route (between two consecutive places).</summary>
public sealed class RouteLeg
{
    /// <summary>Metres.</summary>
    public double Distance { get; set; }

    /// <summary>Seconds.</summary>
    public double Duration { get; set; }
}

/// <summary>A day's street route, from the routing service (see plan-map.js).</summary>
public sealed class RouteResult
{
    public double Distance { get; set; }
    public double Duration { get; set; }
    public List<RouteLeg> Legs { get; set; } = [];

    /// <summary>Route points ([lat, lng]) to draw on the map.</summary>
    public double[][] Geometry { get; set; } = [];
}

/// <summary>
/// Asks for the street route between a day's places (on foot, by car or by bike) and keeps
/// the answers: the same route is not requested again while the page is open.
/// </summary>
public static class Routing
{
    private static readonly Dictionary<string, RouteResult?> Cache = [];

    /// <summary>Forgets the stored routes (for the tests).</summary>
    public static void ClearCache() => Cache.Clear();

    /// <summary>Route key: mode and coordinates in order (6 decimal places ≈ 10 cm).</summary>
    public static string Key(IEnumerable<PlanPlaceDto> stops, RouteMode mode) =>
        mode + ":" + string.Join(";", stops.Select(s => FormattableString.Invariant($"{s.Latitude:0.######},{s.Longitude:0.######}")));

    /// <summary>
    /// Route between the stops in order, or <c>null</c> if there is none (e.g. crossing the sea on foot).
    /// Throws when the service does not answer (no internet, for example): then nothing is stored.
    /// </summary>
    public static async Task<RouteResult?> GetAsync(IJSObjectReference module, IReadOnlyList<PlanPlaceDto> stops, RouteMode mode)
    {
        var key = Key(stops, mode);
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var points = stops.Select(s => new[] { s.Latitude, s.Longitude }).ToArray();
        var result = await module.InvokeAsync<RouteResult?>("route", points, mode.ToString());
        if (result is not null && result.Legs.Count != stops.Count - 1)
            result = null; // incomplete answer: keep the straight line


        Cache[key] = result;
        return result;
    }
}
