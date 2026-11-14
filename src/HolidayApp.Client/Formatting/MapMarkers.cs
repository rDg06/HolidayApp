using HolidayApp.Shared;

namespace HolidayApp.Client.Formatting;

/// <summary>
/// Dados dos marcadores enviados ao plan-map.js (mapa do planeador e da página de partilha),
/// para os dois mostrarem os mesmos números, ícones e cores.
/// </summary>
public static class MapMarkers
{
    /// <param name="places">Lista completa (o índice de cada marcador é a posição nela).</param>
    /// <param name="visible">Quais aparecem.</param>
    /// <param name="reference">Quais são só referência (aparecem, mas não puxam o zoom).</param>
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
                label = Pt.Label(x.p.Kind),
                front = x.p.IsLodging,
                reference = reference?.Invoke(x.p) ?? false,
            })
            .ToArray();
    }
}
