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
export async function create(element, dotnet, places, route) {
    await loadLeaflet();
    const L = window.L;

    const map = L.map(element).setView([DEFAULT_VIEW.lat, DEFAULT_VIEW.lng], DEFAULT_VIEW.zoom);
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    }).addTo(map);

    const routeLayer = L.layerGroup().addTo(map);
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
         * na lista completa), `number` (só nas visitas), `kind` e `icon` (SVG feito pela app,
         * no alojamento e nos transportes), `front` (fica por cima) e `reference` (transporte
         * sem dia: aparece, mas fica fora do zoom).
         * `fit` ajusta o mapa para mostrar todos. `route` é a linha do percurso do dia
         * ([lat, lng] pela ordem), vazia quando não há dia escolhido.
         */
        setPlaces(list, fit, route) {
            layer.clearLayers();
            routeLayer.clearLayers();
            if (route && route.length > 1) {
                L.polyline(route, { className: "route-line", weight: 3, dashArray: "6 8", interactive: false }).addTo(routeLayer);
            }

            markers = new Map(list.map((place) => {
                const classes = ["map-pin"];
                if (place.icon) classes.push("has-icon", place.kind);
                if (place.visited) classes.push("visited");
                if (place.reference) classes.push("reference");
                const big = !!place.icon;
                const icon = L.divIcon({
                    className: classes.join(" "),
                    // O ícone é SVG fixo gerado pela app (PlaceIcons.cs), nunca texto do utilizador
                    html: place.icon ? `<span>${place.icon}</span>` : `<span>${place.number}</span>`,
                    iconSize: big ? [32, 32] : [28, 28],
                    iconAnchor: big ? [16, 16] : [14, 14],
                    popupAnchor: [0, -14],
                });

                const marker = L.marker([place.lat, place.lng], {
                    icon,
                    draggable: true,
                    title: place.icon ? `${place.label}: ${place.name}` : place.name,
                    zIndexOffset: place.front ? 1000 : 0, // a casa fica por cima dos outros marcadores
                })
                    .bindPopup(textElement(place.name))
                    .addTo(layer);

                marker.on("dragend", () => {
                    const pos = marker.getLatLng();
                    dotnet.invokeMethodAsync("OnPlaceMoved", place.index, pos.lat, pos.lng);
                });

                marker.isReference = !!place.reference;
                return [place.index, marker];
            }));

            if (fit) api.fitAll();
        },

        fitAll() {
            // Transportes que servem todos os dias não puxam o zoom (ex.: metro de outra cidade)
            const main = [...markers.values()].filter((m) => !m.isReference);
            const all = main.length > 0 ? main : [...markers.values()];
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

    api.setPlaces(places, true, route);
    return api;
}

/** Desloca a página até ao mapa (ex.: ao pôr uma reserva de alojamento no mapa). */
export function scrollIntoView(element) {
    element?.scrollIntoView({ behavior: "smooth", block: "start" });
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

/**
 * Arrastar os locais da lista para mudar a ordem (rato e toque), pela pega `.drag-handle`.
 * Não mexe no HTML (é do Blazor): só desloca o item com `transform`, mostra onde vai ficar
 * e, ao largar, avisa o .NET (OnReorder) com as posições na lista completa (`data-index`).
 * Com o teclado, as setas ↑ ↓ na pega trocam com o anterior/seguinte (OnReorderStep).
 */
export function attachSortable(root, dotnet) {
    let drag = null;

    const items = () => [...root.querySelectorAll("li[data-visit]")];
    const indexOf = (li) => Number(li.dataset.index);

    function clearMarks() {
        root.querySelectorAll(".drop-before, .drop-after").forEach((li) => li.classList.remove("drop-before", "drop-after"));
    }

    function finish(cancelled) {
        if (!drag) return;
        const { item, handle, pointerId, target } = drag;
        drag = null;
        item.classList.remove("dragging");
        item.style.transform = "";
        root.classList.remove("is-sorting");
        clearMarks();
        try {
            if (handle.hasPointerCapture(pointerId)) handle.releasePointerCapture(pointerId);
        } catch {
            // Já libertado
        }
        if (!cancelled && target) dotnet.invokeMethodAsync("OnReorder", indexOf(item), target.index, target.after);
    }

    root.addEventListener("pointerdown", (e) => {
        const handle = e.target.closest(".drag-handle");
        if (!handle || e.button !== 0) return;
        const item = handle.closest("li[data-visit]");
        if (!item) return;

        e.preventDefault();
        try {
            // Continua a receber o movimento mesmo fora da pega
            handle.setPointerCapture(e.pointerId);
        } catch {
            // Ponteiro já terminado: o arrastar funciona na mesma pelos eventos da lista
        }
        drag = {
            item,
            handle,
            pointerId: e.pointerId,
            startY: e.clientY,
            scroller: item.closest(".place-side"),
            startScroll: item.closest(".place-side")?.scrollTop ?? 0,
            target: null,
        };
        item.classList.add("dragging");
        root.classList.add("is-sorting");
    });

    root.addEventListener("pointermove", (e) => {
        if (!drag || e.pointerId !== drag.pointerId) return;
        const { item, scroller } = drag;

        // Aproxima-se da borda da lista com scroll: desloca-a
        if (scroller && scroller.scrollHeight > scroller.clientHeight) {
            const box = scroller.getBoundingClientRect();
            if (e.clientY < box.top + 40) scroller.scrollTop -= 8;
            else if (e.clientY > box.bottom - 40) scroller.scrollTop += 8;
        }

        const scrolled = (scroller?.scrollTop ?? 0) - drag.startScroll;
        item.style.transform = `translateY(${e.clientY - drag.startY + scrolled}px)`;

        // Onde fica: antes do primeiro item cujo meio está abaixo do ponteiro, ou depois do último
        const others = items().filter((li) => li !== item);
        clearMarks();
        drag.target = null;
        if (others.length === 0) return;

        const before = others.find((li) => {
            const r = li.getBoundingClientRect();
            return e.clientY < r.top + r.height / 2;
        });
        const target = before ?? others[others.length - 1];
        const after = !before;
        target.classList.add(after ? "drop-after" : "drop-before");

        // Largar no mesmo sítio não muda nada
        const all = items();
        const from = all.indexOf(item);
        const to = all.indexOf(target);
        if ((!after && to === from + 1) || (after && to === from - 1)) {
            clearMarks();
            return;
        }
        drag.target = { index: indexOf(target), after };
    });

    root.addEventListener("pointerup", (e) => { if (drag && e.pointerId === drag.pointerId) finish(false); });
    root.addEventListener("pointercancel", () => finish(true));

    root.addEventListener("keydown", async (e) => {
        const handle = e.target.closest?.(".drag-handle");
        if (!handle) return;
        if (e.key === "Escape") { finish(true); return; }
        if (e.key !== "ArrowUp" && e.key !== "ArrowDown") return;

        e.preventDefault();
        const item = handle.closest("li[data-visit]");
        const newIndex = await dotnet.invokeMethodAsync("OnReorderStep", indexOf(item), e.key === "ArrowUp" ? -1 : 1);
        // Depois de o Blazor redesenhar, a pega do mesmo local continua com o foco
        requestAnimationFrame(() => root.querySelector(`li[data-index="${newIndex}"] .drag-handle`)?.focus());
    });
}
