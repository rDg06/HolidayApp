// Service worker do HolidayApp: deixa abrir a app e ver os dados sem internet.
//
// Estratégia "rede primeiro": cada pedido vai sempre ao servidor e a resposta é
// guardada; só quando não há ligação é que se usa a última cópia guardada. Assim,
// com internet, vês sempre a versão mais recente. Gravar (POST/PUT/DELETE) precisa
// sempre de ligação.
//
// Fica de fora: o login do App Service (/.auth/), e tudo o que é de outros sites
// (mapas do OpenStreetMap e pesquisa de locais), que seguem as regras desses serviços.

const CACHE = "holidayapp-v1";

self.addEventListener("install", (event) => {
    // Guarda já a página inicial: sem internet, qualquer página da app abre a partir dela
    event.waitUntil((async () => {
        try {
            const response = await fetch("/");
            if (response.ok && response.type === "basic") await (await caches.open(CACHE)).put("/", response);
        } catch {
            // Sem ligação ou à espera do login: fica guardada na próxima visita
        }
        await self.skipWaiting();
    })());
});

self.addEventListener("activate", (event) => {
    event.waitUntil((async () => {
        // Apaga caches de versões antigas do service worker
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

    event.respondWith(networkFirst(request));
});

async function networkFirst(request) {
    const cache = await caches.open(CACHE);
    try {
        const response = await fetch(request);
        // Só guarda respostas completas e normais (sem erros, redirecionamentos para o login, etc.)
        if (response.ok && response.type === "basic" && response.status === 200) {
            await cache.put(request, response.clone());
        }
        return response;
    } catch (error) {
        const cached = await cache.match(request, { ignoreVary: true });
        if (cached) return cached;

        // Páginas (ex.: /ferias/…) sem cópia própria: a app é a mesma, abre a página inicial guardada
        if (request.mode === "navigate") {
            const home = await cache.match("/", { ignoreVary: true });
            if (home) return home;
        }
        throw error;
    }
}
