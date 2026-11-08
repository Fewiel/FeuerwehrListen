// Service Worker – ausschließlich für die PWA-Installierbarkeit (Add-to-Homescreen).
//
// WICHTIG: KEIN Caching und KEINE Ersatz-/Offline-Seite mehr.
//
// Frühere Versionen fingen im fetch-Handler JEDEN fehlgeschlagenen Request ab und lieferten
// eine Fake-Seite ("Offline - Bitte Verbindung prüfen") mit Status 503 zurück. Während eines
// Deploys ist der Server kurz weg (Container-Neustart); dann ersetzte diese Fake-Antwort das
// eigentliche HTML-Dokument. In der iOS-Standalone-PWA (ohne Adressleiste) blieb Safari darauf
// hängen – nur das umständliche Löschen der Website-Daten half.
//
// Jetzt läuft ALLES unverändert ans Netzwerk (network-only, kein respondWith). Kurzzeitige
// Verbindungsabbrüche behandelt die Blazor-Reconnect-Logik in App.razor (Reconnect-Banner +
// automatisches Neuladen), sodass nach einem Update ein normaler Reload genügt.

self.addEventListener('install', function (event) {
    // Neue SW-Version sofort aktivieren (löst die alte, fehlerhafte Version ab).
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil(
        // Etwaige Caches alter SW-Versionen restlos entfernen ...
        caches.keys()
            .then(function (keys) {
                return Promise.all(keys.map(function (key) { return caches.delete(key); }));
            })
            // ... und die Kontrolle über offene Seiten übernehmen.
            .then(function () { return self.clients.claim(); })
    );
});

// Bewusst KEIN fetch-Handler: keine Interception, keine Ersatzantwort.
// Der Browser behandelt alle Requests normal; bei Verbindungsverlust greift die
// Blazor-Reconnect-Behandlung, nicht eine im Service Worker erzeugte Seite.

// Web-Push (Mitgliederbereich): Benachrichtigung anzeigen und beim Antippen die passende Seite oeffnen.
self.addEventListener('push', function (event) {
    var data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = { title: 'Feuerwehr', body: event.data ? event.data.text() : '' }; }
    event.waitUntil(self.registration.showNotification(data.title || 'Feuerwehr', {
        body: data.body || '',
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        data: { url: data.url || '/mein' }
    }));
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    var url = (event.notification.data && event.notification.data.url) || '/mein';
    event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
        for (var i = 0; i < list.length; i++) {
            if ('focus' in list[i]) { list[i].navigate(url); return list[i].focus(); }
        }
        return self.clients.openWindow(url);
    }));
});
