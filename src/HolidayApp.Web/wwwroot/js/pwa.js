// Liga o service worker (app instalável e sem internet) e mostra o aviso de "sem ligação".
(function () {
    if ("serviceWorker" in navigator) {
        window.addEventListener("load", () => {
            navigator.serviceWorker.register("service-worker.js").catch((error) => {
                console.warn("Não foi possível ativar o modo sem internet.", error);
            });
        });
    }

    function updateBanner() {
        const banner = document.getElementById("offline-banner");
        if (banner) banner.hidden = navigator.onLine;
    }

    window.addEventListener("online", updateBanner);
    window.addEventListener("offline", updateBanner);
    document.addEventListener("DOMContentLoaded", updateBanner);
})();
