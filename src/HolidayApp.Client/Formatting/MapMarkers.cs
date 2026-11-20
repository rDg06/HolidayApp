using HolidayApp.Shared;

namespace HolidayApp.Client.Formatting;

/// <summary>
/// Marker data sent to plan-map.js (planner map and share page map),
/// so both show the same numbers, icons and colours.
/// </summary>
public static class MapMarkers
{
    /// <param name="places">Full list (each marker's index is its position in it).</param>
    /// <param name="visible">Which ones are shown.</param>
    /// <param name="reference">Which are only a reference (shown, but do not pull the zoom).</param>
    public static object[] Build(
        IReadOnlyList<PlanPlaceDto> places,
        Func<PlanPlaceDto, bool> visible,
        Func<PlanPlaceDto, bool>? reference = null)
    {
        var numbers = PlanPlaceDto.Numbers(places);
        return places
            .Select((p, i) => (p, i))
            .Where(x => visible(x.p))
            .Select(x => (object)new
            {
                index = x.i,
                number = numbers[x.i],
                name = x.p.Name,
                lat = x.p.Latitude,
                lng = x.p.Longitude,
                visited = x.p.Visited,
                kind = x.p.IsVisit ? null : PlaceIcons.Css(x.p.Kind),
                icon = x.p.IsVisit ? null : PlaceIcons.Svg(x.p.Kind),
                label = Display.Label(x.p.Kind),
                front = x.p.IsLodging,
                reference = reference?.Invoke(x.p) ?? false,
            })
            .ToArray();
    }
}
