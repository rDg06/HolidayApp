// Map of the places to visit on the "Plan vacation" page.
// Uses Leaflet with OpenStreetMap tiles (free, no API key) and
// Nominatim (OpenStreetMap) to look places up by name.

// Leaflet 1.9.4 served by the app itself (wwwroot/lib/leaflet), with the official hashes
const LEAFLET_CSS = {
    href: "lib/leaflet/leaflet.css",
    integrity: "sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=",
};

const LEAFLET_JS = {
    src: "lib/leaflet/leaflet.js",
    integrity: "sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=",
};

// Mainland Portugal, when there are no places or destination yet
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
            reject(new Error("Could not load the map. Check your internet connection."));
        };
        document.head.appendChild(script);
    });

    return leafletLoading;
}

/** Safe text for popups (the name is typed by the user: never as HTML). */
function textElement(text) {
    const el = document.createElement("strong");
    el.textContent = text;
    return el;
}

/**
 * Creates the map inside `element`. `dotnet` receives map clicks
 * (OnMapClick) and dragged markers (OnPlaceMoved).
 * With `options.readOnly` (share/PDF page, "Where I've been" map) there are no map
 * clicks and no dragging; if there is a `dotnet`, clicking a marker tells it (OnPlaceClick).
 * `options.fitMaxZoom` limits the zoom when fitting the markers (14 by default).
 * `options.still` (feed): the map does not move or zoom, so scrolling the page with the
 * finger or the mouse wheel over it scrolls the page, not the map.
 */
export async function create(element, dotnet, places, route, options) {
    const readOnly = !!options?.readOnly;
    const still = !!options?.still;
    const fitMaxZoom = options?.fitMaxZoom ?? 14;
    await loadLeaflet();
    const L = window.L;

    const stillOptions = still
        ? { dragging: false, scrollWheelZoom: false, touchZoom: false, doubleClickZoom: false, boxZoom: false, keyboard: false, zoomControl: false }
        : {};
    const map = L.map(element, stillOptions).setView([DEFAULT_VIEW.lat, DEFAULT_VIEW.lng], DEFAULT_VIEW.zoom);
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    }).addTo(map);

    const routeLayer = L.layerGroup().addTo(map);
    const layer = L.layerGroup().addTo(map);
    // Visible markers, by the place's position in the full list
    let markers = new Map();

    if (!readOnly) {
        map.on("click", (e) => dotnet.invokeMethodAsync("OnMapClick", e.latlng.lat, e.latlng.lng));
    }

    // The map needs to know when the card size changes (e.g. rotating the phone)
    const resizeObserver = new ResizeObserver(() => map.invalidateSize());
    resizeObserver.observe(element);

    const api = {
        /**
         * Redraws the markers (only those in the current filter). Each place has `index` (position
         * in the full list), `number` (visits only), `kind` and `icon` (SVG made by the app,
         * for lodging and transport), `front` (stays on top) and `reference` (transport
         * without a day: shown, but left out of the zoom).
         * `fit` adjusts the map to show them all. `route` is the line of the day's route
         * ({ points, exact }), without points when no day is chosen.
         */
        setPlaces(list, fit, route) {
            layer.clearLayers();
            routeLayer.clearLayers();
            // `route`: { points: [[lat, lng]], exact } — exact = route along streets (solid line);
            // otherwise it is the straight line between places (dashed), while computing or without the service
            const points = route?.points ?? [];
            if (points.length > 1) {
                const style = route.exact
                    ? { className: "route-line exact", weight: 4, interactive: false }
                    : { className: "route-line", weight: 3, dashArray: "6 8", interactive: false };
                L.polyline(points, style).addTo(routeLayer);
            }

            markers = new Map(list.map((place) => {
                const classes = ["map-pin"];
                if (place.icon) classes.push("has-icon", place.kind);
                if (place.visited) classes.push("visited");
                if (place.reference) classes.push("reference");
                const big = !!place.icon;
                const icon = L.divIcon({
                    className: classes.join(" "),
                    // The icon is a fixed SVG generated by the app (PlaceIcons.cs), never user text
                    html: place.icon ? `<span>${place.icon}</span>` : `<span>${place.number}</span>`,
                    iconSize: big ? [32, 32] : [28, 28],
                    iconAnchor: big ? [16, 16] : [14, 14],
                    popupAnchor: [0, -14],
                });

                const marker = L.marker([place.lat, place.lng], {
                    icon,
                    draggable: !readOnly,
                    title: place.icon ? `${place.label}: ${place.name}` : place.name,
                    zIndexOffset: place.front ? 1000 : 0, // the house stays on top of the other markers
                })
                    .bindPopup(textElement(place.name))
                    .addTo(layer);

                if (readOnly && dotnet) {
                    marker.on("click", () => dotnet.invokeMethodAsync("OnPlaceClick", place.index));
                }

                if (!readOnly) marker.on("dragend", () => {
                    const pos = marker.getLatLng();
                    dotnet.invokeMethodAsync("OnPlaceMoved", place.index, pos.lat, pos.lng);
                });

                marker.isReference = !!place.reference;
                return [place.index, marker];
            }));

            if (fit) api.fitAll();
        },

        fitAll() {
            // Transport that serves every day does not pull the zoom (e.g. a metro in another city)
            const main = [...markers.values()].filter((m) => !m.isReference);
            const all = main.length > 0 ? main : [...markers.values()];
            if (all.length === 1) {
                map.setView(all[0].getLatLng(), fitMaxZoom);
            } else if (all.length > 1) {
                map.fitBounds(L.latLngBounds(all.map((m) => m.getLatLng())), { padding: [32, 32], maxZoom: fitMaxZoom });
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

/** Scrolls the page to the map (e.g. when putting a lodging booking on the map). */
export function scrollIntoView(element) {
    element?.scrollIntoView({ behavior: "smooth", block: "start" });
}

/** Looks places up by name (Nominatim / OpenStreetMap). Returns at most 6 results. */
export async function search(query) {
    const url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=6&addressdetails=1&accept-language=en&q="
        + encodeURIComponent(query);

    const response = await fetch(url, { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error(`The search failed (${response.status}).`);

    const results = await response.json();
    return results.map((r) => ({
        name: r.name || r.display_name.split(",")[0],
        description: r.display_name,
        country: r.address?.country ?? null,
        lat: Number(r.lat),
        lng: Number(r.lon),
    }));
}

/**
 * Drag places in the list to change the order (mouse and touch), by the `.drag-handle`.
 * Does not touch the HTML (it belongs to Blazor): only moves the item with `transform`, shows
 * where it will land and, on drop, tells .NET (OnReorder) the positions in the full list (`data-index`).
 * With the keyboard, the ↑ ↓ arrows on the handle swap with the previous/next one (OnReorderStep).
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
            // Already released
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
            // Keeps receiving the movement even outside the handle
            handle.setPointerCapture(e.pointerId);
        } catch {
            // Pointer already finished: dragging still works through the list's events
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

        // Near the edge of a scrolling list: scroll it
        if (scroller && scroller.scrollHeight > scroller.clientHeight) {
            const box = scroller.getBoundingClientRect();
            if (e.clientY < box.top + 40) scroller.scrollTop -= 8;
            else if (e.clientY > box.bottom - 40) scroller.scrollTop += 8;
        }

        const scrolled = (scroller?.scrollTop ?? 0) - drag.startScroll;
        item.style.transform = `translateY(${e.clientY - drag.startY + scrolled}px)`;

        // Where it lands: before the first item whose middle is below the pointer, or after the last
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

        // Dropping in the same spot changes nothing
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
        // After Blazor redraws, the handle of the same place keeps the focus
        requestAnimationFrame(() => root.querySelector(`li[data-index="${newIndex}"] .drag-handle`)?.focus());
    });
}

// Street routes: OpenStreetMap routing service (FOSSGIS, the same as openstreetmap.org),
// free for personal use. Receives the coordinates of the day's places, in order.
const ROUTE_PROFILES = {
    Walk: "routed-foot",
    Car: "routed-car",
    Bike: "routed-bike",
};

/**
 * Route between the points ([lat, lng] in order) on foot, by car or by bike.
 * Returns { distance (m), duration (s), legs: [{ distance, duration }], geometry: [[lat, lng]] },
 * or null if there is no route. Throws if the service does not answer.
 */
export async function route(points, mode) {
    const profile = ROUTE_PROFILES[mode] ?? ROUTE_PROFILES.Walk;
    const coords = points.map(([lat, lng]) => `${lng.toFixed(6)},${lat.toFixed(6)}`).join(";");
    const url = `https://routing.openstreetmap.de/${profile}/route/v1/driving/${coords}?overview=simplified&geometries=geojson`;

    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 15000);
    try {
        const response = await fetch(url, { signal: controller.signal, headers: { Accept: "application/json" } });
        if (!response.ok) throw new Error(`The routing service answered ${response.status}.`);

        const data = await response.json();
        const best = data.code === "Ok" ? data.routes?.[0] : null;
        if (!best) return null;

        return {
            distance: best.distance,
            duration: best.duration,
            legs: best.legs.map((leg) => ({ distance: leg.distance, duration: leg.duration })),
            geometry: best.geometry.coordinates.map(([lng, lat]) => [lat, lng]),
        };
    } finally {
        clearTimeout(timer);
    }
}

/** Country of some coordinates, from Nominatim; null if there is none (e.g. at sea). */
export async function country(lat, lng) {
    const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&zoom=3&accept-language=en&lat=${lat}&lon=${lng}`;
    const response = await fetch(url, { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error(`The country lookup failed (${response.status}).`);

    const data = await response.json();
    return data.address?.country ?? null;
}
