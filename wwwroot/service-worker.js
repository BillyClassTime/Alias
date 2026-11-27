"use strict";

const cacheName = "billyclasstime-alias-v1";

const appShell = [
    "/admin",
    "/manifest.webmanifest",
    "/admin/app.css",
    "/admin/lib/bootstrap/css/bootstrap.min.css",
    "/admin/lib/bootstrap/js/bootstrap.bundle.min.js",
    "/icons/icon-192.png",
    "/icons/icon-512.png"
];

self.addEventListener("install", event => {
    event.waitUntil(
        caches
            .open(cacheName)
            .then(cache => cache.addAll(appShell))
    );

    self.skipWaiting();
});

self.addEventListener("activate", event => {
    event.waitUntil(
        caches
            .keys()
            .then(keys =>
                Promise.all(
                    keys
                        .filter(key => key !== cacheName)
                        .map(key => caches.delete(key))
                )
            )
    );

    self.clients.claim();
});

self.addEventListener("fetch", event => {
    if (event.request.method !== "GET") {
        return;
    }

    const requestUrl = new URL(event.request.url);

    if (requestUrl.pathname.startsWith("/api/")) {
        return;
    }

    event.respondWith(
        fetch(event.request)
            .then(response => {
                const copy = response.clone();

                caches
                    .open(cacheName)
                    .then(cache =>
                        cache.put(event.request, copy));

                return response;
            })
            .catch(() => caches.match(event.request))
    );
});