# Plan: Mitgliederbereich mit Dienstausweis + Führungskreis

Stand: 07.11.2026 · Status: **umgesetzt auf `feature/sso-provider` (noch nicht in master)**
Mockups: Canvas „Digitaler Dienstausweis – Mockups“ (Reihen *Mitgliederbereich* und *Führungskreis*, Design A).
Baut auf `LOGIN-ZUGRIFF.md` auf (Login-Pflicht, IP-Whitelist, Modulrechte).

---

## Entscheidungen

| Thema | Entscheidung |
|---|---|
| Ausweis-Design | Variante A (Karte), **ohne PDF** – nur Anzeige + „Vollbild zum Vorzeigen“ |
| Mitglieder-Login | Mitgliedsnummer + Passwort, von überall (zählt als Anmeldung für die Login-Pflicht) |
| Passwort festlegen | Nur am Whitelist-Tablet im Gerätehaus, im **Vergabe-Modus**, den ein angemeldeter Admin startet und betreut. Keine separate Freigabe – das Passwort gilt sofort. |
| Passwort vergessen | Gleicher Weg (Vergabe-Modus), kein Mail-Reset |
| Lichtbild | Mitglied lädt selbst hoch (im Mitgliederbereich); Admin kann ersetzen/löschen |
| Prüfseite extern | Lichtbild + „Gültig“ + Prüfzeitpunkt. Ungültig/unbekannt: keine Personendaten, kein Bild |
| Prüflink | Eigene GUID je Ausweis (`/ausweis/{guid}`), unabhängig von Mitglieds-Id; neu ausstellen = neue GUID, alter Link sofort ungültig |
| Führungskreis | Stufen: kein / großer / kleiner. **Kleiner Kreis steht über dem großen**: klein sieht klein + groß, groß sieht nur groß |
| Sitzungen | Kalendertermine eines neuen Typs „Führungskreis“ mit Kreis-Zuordnung |
| Anwesenheit Sitzung | Öffnet automatisch vor Beginn (wie Dienste, Standard 30 Min.), im Protokoll eingebettet |
| Erledigte Aufgabe | Wird automatisch Punkt der nächsten Sitzung des Kreises |
| Geräte | Mitgliederbereich auf Handy optimiert; Protokoll/Sitzung auf iPad quer |

---

## Teil 1 – Mitgliederbereich & Dienstausweis

### 1.1 Datenmodell (Migration 33)

`Member` erweitern:

| Spalte | Typ | Zweck |
|---|---|---|
| `PasswordHash` | string? | Mitglieder-Login; null = noch kein Zugang |
| `PasswordSetAt` | DateTime? | Anzeige/Audit |
| `PasswordSetByUserId` | int? | welcher Admin den Vergabe-Modus betreut hat |
| `ModuleAccess` | string, Default `""` | wie `User.ModuleAccess` (`attendance, operations, firesafety, firesafety-link, calendar, defects`); Ausweis + Profil hat jeder |
| `LeadershipCircle` | int, Default 0 | 0 = kein, 1 = großer, 2 = kleiner Kreis |
| `Rank` | string? | Dienstgrad (Admin pflegt) |
| `Email` | string? | Protokollversand, vom Mitglied selbst pflegbar |
| `PhotoFile` | string? | Dateiname des Lichtbilds (s. 1.5) |
| `PhotoUpdatedAt` | DateTime? | Cache-Bust |
| `CardPublicId` | string? | GUID des Ausweises (32 Hex, zufällig) |
| `CardValidUntil` | DateTime? | null = unbegrenzt |
| `CardRevokedAt` | DateTime? | gesperrt |
| `FailedLogins`, `LockedUntil` | int, DateTime? | Brute-Force-Schutz |

Ausweis **gültig** ⇔ `IsActive` ∧ `CardPublicId != null` ∧ `CardRevokedAt == null` ∧ (`CardValidUntil == null` ∨ `CardValidUntil >= heute`) ∧ Lichtbild vorhanden.

Neue Tabelle `PasswordEnrollmentSession` (Vergabe-Modus): `Id, Token, AdminUserId, ClientIp, StartedAt, LastActivityAt, EndedAt`.

### 1.2 Anmeldung

- Login-Seite: ein Feld „Mitgliedsnummer oder Benutzername“. Erst Admin-/Benutzerkonto (`User`) prüfen, sonst `Member` mit `MemberNumber` + `PasswordHash`.
- Cookie wie bisher (`FwCookie`), Claims: `AccountType=Member`, `MemberId`, Name `member:{MemberNumber}`.
- `UserAccessService` erweitern: löst `member:*` gegen `Member` auf (aktiv? Passwort gesetzt? `ModuleAccess`, `LeadershipCircle`). Inaktives Mitglied ⇒ Sitzung ungültig (bestehende `OnValidatePrincipal`-Logik).
- Mitglieder sind nie Admin, sehen nie „Listen & Daten“ oder die Verwaltung.
- Neue Passwörter direkt mit PBKDF2 hashen (`VerifyPassword` kennt das Format bereits).
- Sperre nach 5 Fehlversuchen für 15 Min. je Mitgliedsnummer + Rate-Limit je IP.

### 1.3 Passwort-Vergabe am Tablet

1. Admin meldet sich am Whitelist-Tablet an → Verwaltung › „Passwort-Vergabe“ → „Vergabe-Modus starten“ (nur Rolle Admin **und** `IsLoginExemptIp`).
2. Server legt `PasswordEnrollmentSession` an, Token im Cookie `fw_enroll` (HttpOnly). Der Admin bleibt angemeldet; die Seite `/passwort-vergabe` läuft im Vollbild.
3. Mitglied gibt Mitgliedsnummer ein (oder scannt seinen Mitglieds-QR wie bei den Listen) → Anzeige Name + Lichtbild (falls vorhanden) zur Sichtprüfung durch den Admin, „Nicht ich“ bricht ab.
4. Passwort 2× eingeben (mind. 10 Zeichen) → gespeichert, sofort nutzbar. Bildschirm springt zurück auf Schritt 1, keine Daten bleiben sichtbar.
5. Modus endet per Button, nach 30 Min. ohne Eingabe oder beim Logout des Admins.

Serverseitig wird bei jedem Schritt geprüft: Token gültig ∧ Session nicht beendet ∧ gleiche Client-IP ∧ IP auf Whitelist ∧ Admin-Konto noch aktiv. Jede Vergabe wird protokolliert (Mitglied, Admin, Zeit) – sichtbar als „Heute vergeben“.

### 1.4 Seiten (Client, Mitglieder-Layout mit unterer Tab-Leiste)

| Route | Inhalt | Mockup |
|---|---|---|
| `/login` | erweitert um Mitgliedsnummer | M1 |
| `/passwort-vergabe` | Vergabe-Modus (Tablet) | M2 |
| `/admin/passwort-vergabe` | Modus starten/beenden, heutige Vergaben | M3 |
| `/mein` | Start: Ausweis-Kachel, nächste Termine, offene Wachen, Führungskreis-Kachel | M4 |
| `/mein/ausweis` | Ausweiskarte A + QR, Vollbild | M5 |
| `/mein/profil` | Lichtbild hochladen/ändern, E-Mail, Passwort ändern | – |
| `/ausweis/{guid}` | öffentliche Prüfseite | A-Prüfseite |

Tab-Leiste: Start · Ausweis · Wachen · Kalender – Wachen/Kalender nur, wenn freigegeben (gleiche Modulprüfung wie im Rest der App, Server-Sperre inklusive). Nach Mitglieder-Login landet man auf `/mein`; die bestehende Navigation bleibt für Admins/Benutzer unverändert.

### 1.5 Lichtbild

- Upload im Profil: Auswahl/Kamera → Zuschnitt 4:5 im Browser (Canvas, JS-Interop) → JPEG max. 600×750, ~150 KB. Keine neue Server-Bibliothek nötig.
- Server prüft: JPEG-Signatur, max. 1 MB, nur eigenes Profil (Admin: jedes). Ablage unter `<Datenverzeichnis>/photos/{memberId}-{zufall}.jpg` (Docker-Volume, nicht `wwwroot`), alte Datei wird gelöscht.
- Auslieferung nur über Endpunkte: `GET /client-api/me/photo` (angemeldet) und `GET /client-api/card-check/{guid}/photo` (nur wenn Ausweis gültig). `Cache-Control: private`, kein Verzeichnis-Listing.

### 1.6 Ausweis & QR

- `CardPublicId` wird beim ersten Lichtbild-Upload bzw. beim Anlegen des Zugangs erzeugt. Admin-Aktionen in der Mitgliederverwaltung: *Neu ausstellen* (neue GUID), *Sperren*, *Gültig bis* setzen, Dienstgrad pflegen.
- QR-Code serverseitig mit vorhandenem **QRCoder** als SVG: Inhalt `https://<öffentliche Adresse>/ausweis/{guid}`. Basisadresse aus `AppSettings:BaseUrl` (nicht aus dem Host-Header).
- Vollbild-Modus: Bildschirm bleibt an (Wake Lock, wo verfügbar), max. Helligkeit ist Sache des Geräts.

### 1.7 Prüfseite `/ausweis/{guid}`

- Von der Login-Pflicht ausgenommen (`IsLoginGateExemptPath` + Approve-Host-Ausnahme), `noindex`, kein Menü (StandaloneLayout).
- `GET /client-api/card-check/{guid}` → `{ valid: true, checkedAt }` bzw. `{ valid: false }`. Keine weiteren Felder; das Bild nur über den Foto-Endpunkt bei gültigem Ausweis.
- Rate-Limit je IP (z. B. 30/Min.), Antwortzeit für unbekannte GUIDs konstant halten.
- Anzeige: Lichtbild groß, grünes „GÜLTIG“, Prüfzeitpunkt, Hinweis „Lichtbild mit der Person vergleichen“. Ungültig: rotes „NICHT GÜLTIG“, kein Bild, kein Name.

### 1.8 Verwaltung

- Mitgliederverwaltung je Mitglied: Zugang ja/nein (Passwort entfernen), Bereiche (gleiche Auswahl wie bei Benutzern), Führungskreis (kein/groß/klein), Dienstgrad, Ausweis gültig bis / neu ausstellen / sperren, Lichtbild ansehen/löschen.
- Neue Mitglieder haben nur Ausweis + Profil, bis Bereiche gesetzt sind.

---

## Teil 2 – Führungskreis

### 2.1 Rechte

`LeadershipCircle` am Mitglied (s. 1.1): 0 kein, 1 großer, 2 kleiner Kreis. Admins (Benutzerkonten) sehen alles.

Sichtbarkeit einer Sitzung/Aufgabe mit Kreis `c`: sichtbar, wenn `member.LeadershipCircle >= c`.
→ Kleiner Kreis (2) sieht klein + groß, großer Kreis (1) sieht nur groß.

Menü „Führungskreis“ (Protokoll, Aufgaben) erscheint nur bei `LeadershipCircle > 0` oder Admin; Endpunkte `/client-api/leadership/*` sind serverseitig gleich abgesichert (eigenes Modul `leadership` in der Modulprüfung).

### 2.2 Datenmodell (Migration 34)

- `CalendarEventType.Fuehrungskreis = 5`, neue Spalte `CalendarEvent.LeadershipCircle` (int?). Solche Termine sieht im Kalender nur, wer den Kreis sehen darf; für alle anderen tauchen sie nicht auf (auch nicht in `/client-api/open-lists`).
- `AttendanceList.LeadershipProtocolId` (int?) – Anwesenheit der Sitzung nutzt die vorhandenen Listen inkl. „entschuldigt“, taucht aber nicht in offenen/abgeschlossenen Listen auf.
- `LeadershipProtocol`: `Id, CalendarEventId, Circle, Status (Vorbereitung | Läuft | Abgeschlossen), AttendanceListId, StartedAt, ClosedAt, ClosedByMemberId, CreatedAt`.
- `LeadershipItem` (Tagesordnungspunkt): `Id, ProtocolId?, Circle, SortOrder, Title, OwnerMemberId?, Notes, Decision, Status (Offen | Besprochen | Vertagt), SourceTaskId?, CreatedAt`.
  `ProtocolId = null` = „wartet auf nächste Sitzung“ (Rückstau).
- `LeadershipTask`: `Id, Circle, Title, Description, AssigneeMemberId?, DueDate?, Status (Offen | Erledigt), SourceItemId?, CreatedByMemberId, CreatedAt, CompletedAt`.

### 2.3 Abläufe

**Termin anlegen** (Kalender, Typ Führungskreis, Kreis wählen) → automatisch `LeadershipProtocol` in *Vorbereitung* mit Standardpunkten „Begrüßung, Anwesenheit, Protokoll der letzten Sitzung“ und „Verschiedenes“; alle Rückstau-Punkte des Kreises werden übernommen.

**Protokoll vorbereiten** (F2): Punkte anlegen, sortieren (Ziehen), Verantwortliche setzen, offene Aufgaben per „+“ als Punkt übernehmen (Punkt bekommt `SourceTaskId`). Optional „Tagesordnung versenden“ (Mail an Kreis).

**Aufgabe erledigt** (F1) → neuer `LeadershipItem` „Erledigt: {Titel}“ mit `SourceTaskId`, eingehängt in das nächste Protokoll des Kreises im Status *Vorbereitung*; gibt es noch keins, als Rückstau (`ProtocolId = null`). Doppelte Übernahme verhindern (gleiche `SourceTaskId`).

**Sitzung** (F3): `ScheduledListBackgroundService` öffnet für fällige Führungskreis-Termine (wie bei Diensten über `MinutesBeforeEvent`) die Anwesenheitsliste und setzt das Protokoll auf *Läuft*. Anwesenheit: Kandidaten = Mitglieder des Kreises (bei großem Kreis inkl. kleinem), Tippen schaltet anwesend → entschuldigt → fehlt. Je Punkt: Notizen, Beschluss, Aufgaben anlegen (`SourceItemId`), Status setzen; „Vertagt“ schiebt den Punkt in den Rückstau.

**Abschließen**: Protokoll + Anwesenheit schließen, PDF erzeugen (`PdfExportService`, neues Layout), per Mail an alle Kreismitglieder mit E-Mail. Danach nur noch lesbar; Korrekturen nur Admin.

### 2.4 Seiten

| Route | Inhalt | Mockup |
|---|---|---|
| `/fuehrungskreis/aufgaben` | Liste mit Filtern (offen, meine, erledigt, Kreis), anlegen, erledigen | F1 |
| `/fuehrungskreis/protokolle` | anstehende + vergangene Sitzungen | – |
| `/fuehrungskreis/protokoll/{id}` | Vorbereitung (F2) bzw. laufende Sitzung (F3) je nach Status; abgeschlossen = Leseansicht + PDF | F2, F3 |

Notizen werden während der Sitzung automatisch gespeichert (debounced, mit „gespeichert“-Anzeige), damit ein Verbindungsabbruch am iPad nichts kostet.

---

## Umsetzungsreihenfolge

1. **Mitglieder-Login**: Migration 33 (Login-Spalten), `UserAccessService`/Login erweitert, Sperre/Rate-Limit.
2. **Passwort-Vergabe-Modus** am Tablet (M2/M3).
3. **Profil + Lichtbild-Upload**, Mitglieder-Layout mit `/mein` (M4).
4. **Ausweis + QR + Prüfseite** (M5, A-Prüfseite), Admin-Felder in der Mitgliederverwaltung.
5. **Führungskreis-Grundlage**: Migration 34, Kreis am Mitglied, Kalendertyp + Sichtbarkeit.
6. **Aufgaben** (F1) inkl. Automatik „erledigt → Punkt“.
7. **Protokoll vorbereiten** (F2).
8. **Sitzung** (F3): Auto-Anwesenheit, Notizen, Aufgaben aus Punkten, Abschluss mit PDF + Mail.

Jeder Schritt einzeln lauffähig und ohne Wirkung auf bestehende Abläufe (Kiosk, Listen, Admin-Bereich).

## Tests je Schritt

- Login: Mitgliedsnummer/Passwort, gesperrtes/inaktives Mitglied, Sperre nach Fehlversuchen, Kollision Benutzername vs. Mitgliedsnummer.
- Vergabe: ohne Admin / ohne Whitelist-IP / abgelaufene Session → abgelehnt.
- Prüfseite: gültig, gesperrt, abgelaufen, neu ausgestellt (alte GUID), unbekannte GUID; kein Personendatum bei ungültig.
- Rechte: Mitglied ohne Kalender sieht keinen Kalender (Menü + Route + API); großer Kreis sieht keine Sitzung/Aufgabe des kleinen Kreises, kleiner Kreis sieht beide.
- Aufgabe erledigt → genau ein Punkt in der nächsten Sitzung bzw. im Rückstau.

---

## Umsetzungsstand / Abweichungen vom Plan

- Alle 8 Schritte umgesetzt (Migrationen 33 + 34, Endpunkte in `Endpoints/MemberAreaEndpoints.cs` und `Endpoints/LeadershipEndpoints.cs`, Logik in `Services/LeadershipService.cs`).
- Passwort-Vergabe-Sitzung liegt **im Speicher** statt in einer Tabelle: ein Neustart beendet den Modus. Protokolliert wird je Mitglied (`PasswordSetAt`, `PasswordSetByUserId`).
- Ausweis-Nr. = Mitgliedsnummer.
- Tagesordnung sortieren per Pfeil-Buttons statt Ziehen (zuverlässiger auf alten iPads).
- Führungskreis-Sitzungen nur als Einzeltermine, nicht als Serie.
- Beim Abschließen wandern nicht besprochene und vertagte Punkte automatisch in die nächste Sitzung des Kreises.
- Mitglieder dürfen im Kalender nur eigene Termine und Sitzungen ihres Kreises ändern/stornieren, keine Serien anlegen.

## Testumgebung

Beispieldaten: `python3 tools/seed-testdata.py http://127.0.0.1:5180` gegen eine frisch migrierte Test-DB
(Aufruf von 127.0.0.1, da die Passwort-Vergabe ein Whitelist-Gerät braucht). Legt Mitglieder mit
Zugang/Lichtbild, Wachen, Dienste, drei Führungskreis-Sitzungen (eine abgeschlossen) und Aufgaben an.
