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
    let markers = [];

    map.on("click", (e) => dotnet.invokeMethodAsync("OnMapClick", e.latlng.lat, e.latlng.lng));

    // O mapa precisa de saber quando o tamanho do cartão muda (ex.: rodar o telemóvel)
    const resizeObserver = new ResizeObserver(() => map.invalidateSize());
    resizeObserver.observe(element);

    const api = {
        /** Redesenha os marcadores. `fit` ajusta o mapa para mostrar todos. */
        setPlaces(list, fit) {
            layer.clearLayers();
            markers = list.map((place, index) => {
                const icon = L.divIcon({
                    className: place.visited ? "map-pin visited" : "map-pin",
                    html: `<span>${index + 1}</span>`,
                    iconSize: [28, 28],
                    iconAnchor: [14, 14],
                    popupAnchor: [0, -14],
                });

                const marker = L.marker([place.lat, place.lng], { icon, draggable: true, title: place.name })
                    .bindPopup(textElement(place.name))
                    .addTo(layer);

                marker.on("dragend", () => {
                    const pos = marker.getLatLng();
                    dotnet.invokeMethodAsync("OnPlaceMoved", index, pos.lat, pos.lng);
                });

                return marker;
            });

            if (fit) api.fitAll();
        },

        fitAll() {
            if (markers.length === 1) {
                map.setView(markers[0].getLatLng(), 14);
            } else if (markers.length > 1) {
                map.fitBounds(L.latLngBounds(markers.map((m) => m.getLatLng())), { padding: [32, 32] });
            }
        },

        focus(index) {
            const marker = markers[index];
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
