// Minimal service worker for PWA installation
// No offline caching — the app requires SignalR (live connection)
self.addEventListener('install', function(event) {
    self.skipWaiting();
});

self.addEventListener('activate', function(event) {
    event.waitUntil(clients.claim());
});

// Network-first: pass all requests through, no caching
self.addEventListener('fetch', function(event) {
    event.respondWith(
        fetch(event.request).catch(function() {
            // If offline, return a simple error page
            return new Response('Offline - Bitte Verbindung prüfen', {
                status: 503,
                headers: { 'Content-Type': 'text/plain; charset=utf-8' }
            });
        })
    );
});
