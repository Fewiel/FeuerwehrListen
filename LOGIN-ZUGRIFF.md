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

Ist die Login-Pflicht an und kommt die Anfrage **nicht** von einer Whitelist-Adresse oder
einem Gerätekonto (s. u.):

* Seitenaufrufe werden nach `/login?returnUrl=…` umgeleitet,
* `/client-api/*` antwortet mit `401 {"error":"login_required"}`,
* das Menü zeigt nur noch den Login.

Immer offen bleiben: `/login`, die Anmelde-Endpunkte (`/client-api/auth/login|qr|complete|logout|me`),
`/client-api/app-context` (ohne Einheiten-Namen), Freigabe-Links (`/approve/…`), die öffentliche
Ausweisprüfung (`/ausweis/{id}`, `/client-api/card-check/*` – nur Lichtbild + gültig/ungültig),
der SSO-Provider (`/sso/*`, leitet selbst zum Login), die externe API (`/api/*`, eigener API-Key)
sowie das Blazor-Framework und statische Dateien (enthalten keine Daten).

Jede andere Seiten-Route leitet zum Login um – unabhängig vom `Accept`-Header.

Von einer Whitelist-Adresse verhält sich das Tool wie bisher (Gäste ohne Login,
Netz-Schranke und Host-Profile wirken weiter).

**Server-Modus (alte Geräte, SignalR):** Im laufenden Circuit gibt es keinen HttpContext.
Die Client-IP wird deshalb beim ersten Seitenaufruf mit Data Protection versiegelt als
Komponenten-Parameter mitgegeben (`CircuitClientContext`) und im Circuit wieder entsiegelt –
der Browser kann den Wert nicht fälschen. Fehlt sie, gilt der Self-Call als „nicht von der
Whitelist“ (fail-closed).

**Client-Adresse:** Gewertet wird die direkte Verbindungsadresse. Nur wenn ein
Reverse-Proxy unter „Adressen des Reverse-Proxys“ eingetragen ist, wird
`X-Forwarded-For` ausgewertet. Die Diagnose „Zugriff prüfen“ zeigt, welche Adresse
die App sieht und ob sie auf der Whitelist steht. Im Server-Modus (Alt-Geräte) reicht
der Server die Nutzer-IP intern als `X-Fw-Client-Ip` durch (nur zusammen mit dem
prozess-geheimen `X-Fw-Internal` gültig).

## Login-Bremse (Brute-Force-Schutz)

Für alle Anmeldewege (Benutzer/Mitglieder/Geräte-Login, QR-Login inkl. Admin-PIN, API-Key,
SSO-Client-Secret), `LoginThrottle`:

* Jeder Fehlversuch wird künstlich verzögert – 300 ms je Fehlversuch auf das Konto plus
  50 ms je Fehlversuch der IP, höchstens 4 s. Zehn Fehlversuche kosten so ~20 s statt
  Millisekunden.
* 10 Fehlversuche auf ein Konto → Konto 15 Minuten gesperrt (auch für den Richtigen, HTTP 429
  mit `until`). 50 Fehlversuche von einer IP → IP 15 Minuten gesperrt. Erfolg setzt den
  Konto-Zähler zurück; Admin kann über `POST /client-api/admin/users/{id}/unlock` bzw.
  „entsperren“ beim Mitglied aufheben.
* Unbekannte Konten prüfen gegen einen Dummy-Hash, damit die Antwortzeit nicht verrät, ob es
  den Namen gibt.
* Mitglieder haben zusätzlich die dauerhafte DB-Sperre (5 Fehlversuche → 15 Minuten).
* Prüf-Links (`/client-api/card-check/*`) sind separat auf 30 Aufrufe/Minute je IP begrenzt.

Zähler liegen im Speicher (Neustart löscht sie) – als Bremse ausreichend.

## Gerätekonten (ELW, KdoW, Tablets mit wechselnder IP)

*Verwaltung → Benutzer → Rolle „Gerät“*

Für Geräte, die ohne Login auf die Oberfläche sollen, deren IP aber wechselt (LTE-Router im
ELW/KdoW, Tablet im WLAN). Ablauf:

1. Gerätekonto anlegen (`elw1`, `tablet-gh` …), Bereiche wählen (Anwesenheit, Einsatz, Wachen,
   Mängel, Kalender), optional „Passwort-Vergabe erlaubt“ (nur Gerätehaus-Tablet). Langes
   generiertes Passwort, nur der Admin kennt es.
2. Auf dem Gerät einmal über die normale Login-Seite mit dem Gerätekonto anmelden. Der Server
   setzt ein eigenes Cookie `fw_device` (2 Jahre, HttpOnly) – **kein** Benutzer-Cookie.
3. Das Gerät gilt ab dann als vertrauenswürdig wie im Feuerwehrnetz: Kiosk-Ansicht mit den
   freigegebenen Bereichen, keine Login-Umleitung, Netz-Schranke greift nicht. Es ist aber
   **nicht angemeldet**: keine verwaltenden Aktionen (Listen schließen …), keine Verwaltung,
   kein Mitgliederbereich, kein Führungskreis, keine externe API.
4. Mitglieder/Admins melden sich auf dem Gerät zusätzlich normal an; nach dem Abmelden fällt
   das Gerät in den Kiosk-Zustand zurück (nicht auf den Login).

Kontrolle: Die Benutzerliste zeigt je Gerätekonto „n Geräte angemeldet“, zuletzt gesehen und
letzte IP. „Alle Geräte abmelden“, Passwortwechsel, Rollenwechsel oder Löschen sperren jede
Sitzung sofort (Token-Hash in `DeviceSession`, `RevokedAt`; 60 s Cache wird geleert).

Hinweise: Safari „Verlauf und Websitedaten löschen“ oder ein Zurücksetzen des Geräts entfernt
das Cookie → einfach neu anmelden. Liegt die App als Web-App auf dem Home-Bildschirm, dort
anmelden (iOS trennt die Daten von Safari). Alte iPads (Server-Modus): das Geräte-Cookie wird
wie die Client-IP versiegelt in den Circuit gereicht (`X-Fw-Device`, nur mit `X-Fw-Internal`).

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

## Mitglieder-Konten (Mitgliederbereich)

Mitglieder melden sich mit Mitgliedsnummer + Passwort an (Rolle `Member`). Sie erhalten nur ihre
freigegebenen Bereiche und niemals:

* die externe API ohne Key (`/api/*`), Export-Token (`/token`),
* verwaltende Aktionen: Listen/Wachen schließen, Entschuldigungen, Einträge löschen, Adresse
  ändern, Serien, Anwesenheitslisten aus Terminen anlegen (`NetworkAccessService.IsMemberForbiddenPath`),
* „Listen & Daten“, Verwaltung, SSO-Login in Fremdsysteme.

## Sicherheitsprüfung (QA, 07.11.2026)

`qa.py`/`qa2.py` (siehe Testumgebung) prüfen 88 Fälle: anonym von außen (Seiten → Login,
alle `/client-api` → 401, Header-Spoofing wirkungslos, Prüflinks offen ohne Personendaten),
Mitglieder-Grenzen, Führungskreis-Sichtbarkeit (groß sieht klein nicht), Passwort-Vergabe nur
von Whitelist-Gerät und nur in derselben Sitzung, Server-Modus (Kiosk über Whitelist offen,
von außen Login). Alle bestanden. Hinweis: `/swagger` ist nur im Development-Modus erreichbar.
