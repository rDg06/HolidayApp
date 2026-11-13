// Mapa dos locais a visitar na página "Organizar férias".
// Usa Leaflet com os mapas do OpenStreetMap (gratuitos, sem chave de API) e o
// Nominatim (OpenStreetMap) para procurar locais pelo nome.

// Leaflet 1.9.4 servido pela própria app (wwwroot/lib/leaflet), com as hashes oficiais
const LEAFLET_CSS = {
    href: "lib/leaflet/leaflet.css",
    integrity: "sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=",
};

const LEAFLET_JS = {
    src: "lib/leaflet/leaflet.js",
    integrity: "sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=",
};

// Portugal continental, quando ainda não há locais nem destino
const DEFAULT_VIEW = { lat: 39.6, lng: -8.0, zoom: 6 };

let leafletLoading = null;

function loadLeaflet() {
    if (window.L) return Promise.resolve();

    leafletLoading ??= new Promise((resolve, reject) => {
        const css = document.createElement("link");
        css.rel = "stylesheet";
        css.href = LEAFLET_CSS.href;
        css.integrity = LEAFLET_CSS.integrity;
        css.crossOrigin = "";
        document.head.appendChild(css);

        const script = document.createElement("script");
        script.src = LEAFLET_JS.src;
        script.integrity = LEAFLET_JS.integrity;
        script.crossOrigin = "";
        script.onload = () => resolve();
        script.onerror = () => {
            leafletLoading = null;
            reject(new Error("Não foi possível carregar o mapa. Verifica a ligação à internet."));
        };
        document.head.appendChild(script);
    });

    return leafletLoading;
}

// Ícone de casa do alojamento (o mesmo desenho do PlaceMap.razor)
const HOUSE_SVG = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3.2 2.5 11h2.7v9.3h5.3v-6h3v6h5.3V11h2.7z" fill="currentColor"/></svg>';

/** Texto seguro para popups (o nome é escrito pelo utilizador: nunca como HTML). */
function textElement(text) {
    const el = document.createElement("strong");
    el.textContent = text;
    return el;
}

/**
 * Cria o mapa dentro de `element`. `dotnet` recebe os cliques no mapa
 * (OnMapClick) e os marcadores arrastados (OnPlaceMoved).
 */
export async function create(element, dotnet, places) {
    await loadLeaflet();
    const L = window.L;

    const map = L.map(element).setView([DEFAULT_VIEW.lat, DEFAULT_VIEW.lng], DEFAULT_VIEW.zoom);
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    }).addTo(map);

    const layer = L.layerGroup().addTo(map);
    // Marcadores visíveis, pela posição do local na lista completa
    let markers = new Map();

    map.on("click", (e) => dotnet.invokeMethodAsync("OnMapClick", e.latlng.lat, e.latlng.lng));

    // O mapa precisa de saber quando o tamanho do cartão muda (ex.: rodar o telemóvel)
    const resizeObserver = new ResizeObserver(() => map.invalidateSize());
    resizeObserver.observe(element);

    const api = {
        /**
         * Redesenha os marcadores (só os do filtro atual). Cada local traz `index` (posição
         * na lista completa), `number` (null no alojamento) e `lodging`.
         * `fit` ajusta o mapa para mostrar todos.
         */
        setPlaces(list, fit) {
            layer.clearLayers();
            markers = new Map(list.map((place) => {
                const className = place.lodging ? "map-pin lodging" : place.visited ? "map-pin visited" : "map-pin";
                const icon = L.divIcon({
                    className,
                    html: place.lodging ? `<span>${HOUSE_SVG}</span>` : `<span>${place.number}</span>`,
                    iconSize: place.lodging ? [32, 32] : [28, 28],
                    iconAnchor: place.lodging ? [16, 16] : [14, 14],
                    popupAnchor: [0, -14],
                });

                const marker = L.marker([place.lat, place.lng], {
                    icon,
                    draggable: true,
                    title: place.name,
                    zIndexOffset: place.lodging ? 1000 : 0, // a casa fica por cima dos outros marcadores
                })
                    .bindPopup(textElement(place.name))
                    .addTo(layer);

                marker.on("dragend", () => {
                    const pos = marker.getLatLng();
                    dotnet.invokeMethodAsync("OnPlaceMoved", place.index, pos.lat, pos.lng);
                });

                return [place.index, marker];
            }));

            if (fit) api.fitAll();
        },

        fitAll() {
            const all = [...markers.values()];
            if (all.length === 1) {
                map.setView(all[0].getLatLng(), 14);
            } else if (all.length > 1) {
                map.fitBounds(L.latLngBounds(all.map((m) => m.getLatLng())), { padding: [32, 32] });
            }
        },

        focus(index) {
            const marker = markers.get(index);
            if (!marker) return;
            map.setView(marker.getLatLng(), Math.max(map.getZoom(), 15));
            marker.openPopup();
        },

        centerOn(lat, lng, zoom) {
            map.setView([lat, lng], zoom ?? 12);
        },

        dispose() {
            resizeObserver.disconnect();
            map.remove();
        },
    };

    api.setPlaces(places, true);
    return api;
}

/** Procura locais pelo nome (Nominatim / OpenStreetMap). Devolve no máximo 6 resultados. */
export async function search(query) {
    const url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=6&accept-language=pt&q="
        + encodeURIComponent(query);

    const response = await fetch(url, { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error(`A pesquisa falhou (${response.status}).`);

    const results = await response.json();
    return results.map((r) => ({
        name: r.name || r.display_name.split(",")[0],
        description: r.display_name,
        lat: Number(r.lat),
        lng: Number(r.lon),
    }));
}
