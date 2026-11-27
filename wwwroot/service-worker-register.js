"use strict";

if ("serviceWorker" in navigator) {
    window.addEventListener("load", async () => {
        try {
            await navigator.serviceWorker.register(
                "/service-worker.js",
                {
                    scope: "/"
                });

            console.log("Service worker registrado");
        }
        catch (error) {
            console.error(
                "No se pudo registrar el service worker",
                error);
        }
    });
}