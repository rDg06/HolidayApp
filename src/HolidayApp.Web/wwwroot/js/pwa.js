// Registers the service worker (installable app, works offline) and shows the "offline" banner.
(function () {
    if ("serviceWorker" in navigator) {
        window.addEventListener("load", () => {
            navigator.serviceWorker.register("service-worker.js").catch((error) => {
                console.warn("Could not turn on offline mode.", error);

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
