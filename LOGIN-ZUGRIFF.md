# Login-Pflicht, IP-Whitelist und Benutzerrechte

Ersetzt den Passwortschutz im Reverse-Proxy durch einen eigenen Login im Tool.
**Standardmäßig ausgeschaltet** – vorbereitet, aber ohne Wirkung, bis es in den
Einstellungen aktiviert wird.

## Login-Pflicht

*Verwaltung → Einstellungen → Zugriffsschutz*

| Einstellung | Schlüssel | Bedeutung |
|---|---|---|
| Login-Pflicht aktivieren | `Security.LoginRequired` | `true` = ohne Anmeldung ist nichts erreichbar |
| IP-Whitelist (ohne Login) | `Security.LoginWhitelist` | IPs/CIDR-Netze, kommagetrennt, die ohne Anmeldung zugreifen dürfen |

Ist die Login-Pflicht an und kommt die Anfrage **nicht** von einer Whitelist-Adresse:

* Seitenaufrufe werden nach `/login?returnUrl=…` umgeleitet,
* `/client-api/*` antwortet mit `401 {"error":"login_required"}`,
* das Menü zeigt nur noch den Login.

Immer offen bleiben: `/login`, `/client-api/auth/*`, `/client-api/app-context`,
Freigabe-Links (`/approve/…`), der SSO-Provider (`/sso/*`, leitet selbst zum Login),
die externe API (`/api/*`, eigener API-Key) sowie das Blazor-Framework.

Von einer Whitelist-Adresse verhält sich das Tool wie bisher (Gäste ohne Login,
Netz-Schranke und Host-Profile wirken weiter).

**Client-Adresse:** Gewertet wird die direkte Verbindungsadresse. Nur wenn ein
Reverse-Proxy unter „Adressen des Reverse-Proxys“ eingetragen ist, wird
`X-Forwarded-For` ausgewertet. Die Diagnose „Zugriff prüfen“ zeigt, welche Adresse
die App sieht und ob sie auf der Whitelist steht. Im Server-Modus (Alt-Geräte) reicht
der Server die Nutzer-IP intern als `X-Fw-Client-Ip` durch (nur zusammen mit dem
prozess-geheimen `X-Fw-Internal` gültig).

## Benutzerrechte

*Verwaltung → Benutzer*

* **Vollzugang** – ganzes Tool inkl. abgeschlossene Listen und Archiv.
  Alle Bestandsnutzer haben per Migration 32 Vollzugang (`User.ModuleAccess = "*"`).
  Admins haben immer Vollzugang.
* **Nur ausgewählte Bereiche** – für neu angelegte Benutzer frei wählbar:

| Bereich | Schlüssel |
|---|---|
| Anwesenheitslisten | `attendance` |
| Einsatzlisten | `operations` |
| Brandsicherheitswachen (Liste + Detail) | `firesafety` |
| Brandsicherheitswachen nur über Direktlink | `firesafety-link` |
| Kalender | `calendar` |
| Mängel | `defects` |

Nicht freigegebene Bereiche sind im Menü ausgeblendet, auf der Übersicht nicht sichtbar,
als Seite gesperrt (Server leitet direkte Aufrufe zur Übersicht um, der Client zeigt
„Kein Zugriff“) und auf API-Ebene mit `403 module_blocked` gesperrt. „Listen & Daten“
(abgeschlossene Listen, Archiv) gibt es nur mit Vollzugang.

Rechte werden nicht im Cookie gespeichert, sondern je Request nachgeschlagen (60 s Cache,
beim Speichern sofort invalidiert) – Änderungen wirken ohne Neu-Login. Gelöschte Benutzer
verlieren ihre Sitzung sofort.

## Direktlink für Brandsicherheitswachen

Jede Wache hat eine zufällige `PublicId` (GUID, Altdaten per Migration 32 befüllt).
Auf der Wachen-Detailseite kopiert „Direktlink kopieren“ die Adresse `/wache/{PublicId}`.
Über den Link kann man sich eintragen; Abschließen, Bearbeiten und Austragen gehen nur über
die normale Wachen-Seite. Endpunkte: `GET /client-api/firesafetywatch-link/{publicId}`,
`POST /client-api/firesafetywatch-link/{publicId}/register`.
