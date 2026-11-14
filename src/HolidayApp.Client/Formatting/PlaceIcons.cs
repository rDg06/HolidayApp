using HolidayApp.Shared;

namespace HolidayApp.Client.Formatting;

/// <summary>
/// Ícones dos tipos de local (SVG 24×24, cor do texto). Usados na lista, no plano dia a dia
/// e nos marcadores do mapa (enviados ao plan-map.js), para serem iguais em todo o lado.
/// </summary>
public static class PlaceIcons
{
    private const string Open = """<svg class="place-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">""";

    public static string Svg(PlaceKind kind) => kind switch
    {
        PlaceKind.Alojamento => Open + """<path fill="currentColor" d="M12 3.2 2.5 11h2.7v9.3h5.3v-6h3v6h5.3V11h2.7z"/></svg>""",
        PlaceKind.Aeroporto => Open + """<path fill="currentColor" d="M12 2.5c.8 0 1.4.7 1.4 1.5v5.2l8.1 4.6v2.1l-8.1-2.4v4.8l2.1 1.5v1.7L12 20.6l-3.5.9v-1.7l2.1-1.5v-4.8l-8.1 2.4v-2.1l8.1-4.6V4c0-.8.6-1.5 1.4-1.5z"/></svg>""",
        PlaceKind.Metro => Open + """<path fill="none" stroke="currentColor" stroke-width="2.8" stroke-linecap="round" stroke-linejoin="round" d="M5 18.5V5.5l7 7.5 7-7.5v13"/></svg>""",
        PlaceKind.Autocarro => Open + """<path fill="currentColor" fill-rule="evenodd" d="M6.5 2.5h11A2.5 2.5 0 0 1 20 5v12a2 2 0 0 1-1 1.7V20a1 1 0 0 1-1 1h-1.2a1 1 0 0 1-1-1v-1H8.2v1a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1v-1.3A2 2 0 0 1 4 17V5a2.5 2.5 0 0 1 2.5-2.5zM6.3 6v5.5h11.4V6zm1.5 8a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8zm8.4 0a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8z"/></svg>""",
        PlaceKind.Comboio => Open + """<path fill="currentColor" fill-rule="evenodd" d="M8 2h8a4 4 0 0 1 4 4v9a3 3 0 0 1-2.1 2.9l1.7 3.1h-2.3l-1.5-2.8H8.2L6.7 21H4.4l1.7-3.1A3 3 0 0 1 4 15V6a4 4 0 0 1 4-4zM6.4 6.3v4.5h11.2V6.3zm1.6 7a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8zm8 0a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8z"/></svg>""",
        // Local a visitar: alfinete do mapa
        _ => Open + """<path fill="currentColor" fill-rule="evenodd" d="M12 2a7 7 0 0 0-7 7c0 5.3 7 13 7 13s7-7.7 7-13a7 7 0 0 0-7-7zm0 4.3a2.7 2.7 0 1 0 0 5.4 2.7 2.7 0 0 0 0-5.4z"/></svg>""",
    };

    /// <summary>Classe CSS da cor de cada tipo (kind-alojamento, kind-metro…).</summary>
    public static string Css(PlaceKind kind) => "kind-" + kind.ToString().ToLowerInvariant();

    /// <summary>Tipos pela ordem do menu.</summary>
    public static readonly PlaceKind[] All =
        [PlaceKind.Visita, PlaceKind.Alojamento, PlaceKind.Aeroporto, PlaceKind.Metro, PlaceKind.Autocarro, PlaceKind.Comboio];
}
