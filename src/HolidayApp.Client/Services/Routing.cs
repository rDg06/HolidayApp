using HolidayApp.Shared;
using Microsoft.JSInterop;

namespace HolidayApp.Client.Services;

/// <summary>Um troço do caminho (entre dois locais seguidos).</summary>
public sealed class RouteLeg
{
    /// <summary>Metros.</summary>
    public double Distance { get; set; }

    /// <summary>Segundos.</summary>
    public double Duration { get; set; }
}

/// <summary>Caminho por ruas de um dia, vindo do serviço de rotas (ver plan-map.js).</summary>
public sealed class RouteResult
{
    public double Distance { get; set; }
    public double Duration { get; set; }
    public List<RouteLeg> Legs { get; set; } = [];

    /// <summary>Pontos do caminho ([lat, lng]) para desenhar no mapa.</summary>
    public double[][] Geometry { get; set; } = [];
}

/// <summary>
/// Pede o caminho por ruas entre os locais de um dia (a pé, de carro ou de bicicleta) e guarda
/// as respostas: o mesmo percurso não volta a ser pedido enquanto a página estiver aberta.
/// </summary>
public static class Routing
{
    private static readonly Dictionary<string, RouteResult?> Cache = [];

    /// <summary>Esquece os caminhos guardados (para os testes).</summary>
    public static void ClearCache() => Cache.Clear();

    /// <summary>Chave do percurso: modo e coordenadas pela ordem (6 casas decimais ≈ 10 cm).</summary>
    public static string Key(IEnumerable<PlanPlaceDto> stops, RouteMode mode) =>
        mode + ":" + string.Join(";", stops.Select(s => FormattableString.Invariant($"{s.Latitude:0.######},{s.Longitude:0.######}")));

    /// <summary>
    /// Caminho entre as paragens pela ordem, ou <c>null</c> se não houver caminho (ex.: atravessar o mar a pé).
    /// Lança exceção quando o serviço não responde (sem internet, por exemplo): aí não fica guardado.
    /// </summary>
    public static async Task<RouteResult?> GetAsync(IJSObjectReference module, IReadOnlyList<PlanPlaceDto> stops, RouteMode mode)
    {
        var key = Key(stops, mode);
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var points = stops.Select(s => new[] { s.Latitude, s.Longitude }).ToArray();
        var result = await module.InvokeAsync<RouteResult?>("route", points, mode.ToString());
        if (result is not null && result.Legs.Count != stops.Count - 1)
            result = null; // resposta incompleta: fica a linha reta

        Cache[key] = result;
        return result;
    }
}
