// HolidayApp service worker: lets the app open and show the data without internet.
//
// "Network first" strategy: every request always goes to the server and the response is
// stored; only when there is no connection is the last stored copy used. So, online,
// you always see the latest version. Saving (POST/PUT/DELETE) always needs a connection.
//
// Left out: sign-in (the login, register and logout pages, and App Service's /.auth/),
// and everything from other sites (OpenStreetMap tiles, place search and routes), which
// follow those services' rules. Redirects are never stored (e.g. "no session, go to the
// login page"): serving them from the cache broke the app.

// v3: English routes. Changing the name deletes the old caches (see "activate").
const CACHE = "holidayapp-v3";

/** Sign-in pages: always go to the network, no cache. */
const ACCOUNT_PATHS = ["/login", "/register", "/logout"];

/** A response that can be stored: complete, from this site, and not a redirect. */
const cacheable = (response) =>
    response.ok && response.status === 200 && response.type === "basic" && !response.redirected;

self.addEventListener("install", (event) => {
    // Stores the home page right away: offline, any page of the app opens from it
    event.waitUntil((async () => {
        try {
            const response = await fetch("/", { redirect: "manual" });
            if (cacheable(response)) await (await caches.open(CACHE)).put("/", response);
        } catch {
            // Offline or waiting for sign-in: it is stored on the next visit
        }
        await self.skipWaiting();
    })());
});

self.addEventListener("activate", (event) => {
    event.waitUntil((async () => {
        // Deletes caches from older versions of the service worker
        for (const name of await caches.keys()) {
            if (name !== CACHE) await caches.delete(name);
        }
        await self.clients.claim();
    })());
});

self.addEventListener("fetch", (event) => {
    const request = event.request;
    if (request.method !== "GET") return;

    const url = new URL(request.url);
    if (url.origin !== self.location.origin || url.pathname.startsWith("/.auth/")) return;
    if (ACCOUNT_PATHS.some((path) => url.pathname === path)) return;

    event.respondWith(networkFirst(request));
});

async function networkFirst(request) {
    const cache = await caches.open(CACHE);
    try {
        const response = await fetch(request);
        // Only stores complete, normal responses (no errors and no redirects to the login page)
        if (cacheable(response)) {
            await cache.put(request, response.clone());
        }
        return response;
    } catch (error) {
        const cached = await cache.match(request, { ignoreVary: true });
        if (cached && !cached.redirected) return cached;

        // Pages (e.g. /vacations/…) without their own copy: it is the same app, open the stored home page
        if (request.mode === "navigate") {
            const home = await cache.match("/", { ignoreVary: true });
            if (home && !home.redirected) return home;
        }
        throw error;
    }
}
