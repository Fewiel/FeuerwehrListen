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
