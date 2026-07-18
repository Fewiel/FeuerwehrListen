# CLAUDE.md — FeuerwehrListen

Projekt-Memory für Claude Code. Kurz, faktenbasiert, auf Architektur-Entscheidungen
fokussiert. Antworten/Commits auf **Deutsch** (Projektsprache, deutsche Feuerwehr-Domäne).

## Was ist das

Webanwendung zur Verwaltung von **Anwesenheits-, Einsatz-, Brandsicherheitswacht- und
Mängellisten** für Feuerwehren. Kiosk-/Gemeinschaftsgeräte im LAN (Tablets/iPads am
Gerätehaus), zusätzlich Admin-Bereich. Läuft per Docker auf einem Ubuntu-Server, SQLite-DB
in Docker-Volume, Auto-Update per Cron alle 5 Min vom `master`-Branch.

## Tech-Stack

- **.NET 8**, **Blazor Web App** mit **global Interactive WebAssembly** (kein SignalR im Normalbetrieb)
- **linq2db 5.4** (ORM, `AppDbConnection : DataConnection`) — SQLite (Default) oder MySQL
- **FluentMigrator 5.2** — Migrationen laufen beim Start automatisch (`runner.MigrateUp()`)
- **PdfSharp 6** (PDF-Export), **QRCoder** (QR-Codes/Mitglieder-Tags)
- **Swashbuckle** (Swagger, nur Development)
- Tests: **NUnit + Playwright** (E2E, Chromium)
- Frontend-Assets **komplett lokal** (Bootstrap, bootstrap-icons, barcode-detector-Polyfill) —
  Projekt-Regel: nichts vom CDN laden.

## Projektstruktur (3 Projekte, `FeuerwehrListen.sln`)

- **`FeuerwehrListen/`** — Server (ASP.NET Core Host). Enthält:
  - `Program.cs` — **groß (~1700 Zeilen)**, DI + Middleware + **alle `/client-api/*`-Minimal-API-Endpoints** inline
  - `Controllers/` — externe **`/api`**-REST-API (X-API-Key)
  - `Models/`, `DTOs/`, `Repositories/` (ein Repo pro Entity), `Services/`, `Migrations/`, `Data/`
- **`FeuerwehrListen.Client/`** — Blazor **WASM** Client: `Pages/`, `Pages/Admin/`, `Layout/`,
  `Components/`, `Services/` (`AppContextService`, `CookieAuthStateProvider`), `Models/` (DTOs)
- **`FeuerwehrListen.Tests/`** — Playwright-E2E, nach Phasen sortiert (`Phase1..13_*Tests.cs`),
  `BaseTest.cs` (Login-/CRUD-Helper), `GlobalSetup`/`TestServerFixture` (startet echten Server)

## Zwei getrennte APIs — NICHT verwechseln

1. **`/api/*`** (Controller) — **externe** REST-API, Auth per **`X-API-Key`**-Header
   (`ApiKeyAuthMiddleware` matcht `StartsWithSegments("/api")`). Doku: `API-DOCUMENTATION.md`.
   Bleibt bewusst stabil/unangetastet.
2. **`/client-api/*`** (Minimal-API in `Program.cs`) — **interner** WASM-Client. Zustandslos, JSON.
   Matcht die ApiKey-Middleware **nicht**. Gruppen:
   - anonym: `open-lists`, `app-context`, Eintragen/Melden (Kiosk)
   - `RequireAuthorization()`: Listen-Verwaltung (`listMgmt`-Gruppe)
   - `RequireAuthorization("Admin")`: CRUD (`admin`-Gruppe: vehicles/keywords/functions/apikeys/members/users/statistics)

## Auth-Modell (wichtig, nicht trivial)

- **Cookie-Auth** `fw_auth` (Schema `"FwCookie"`) für den WASM-Admin-Bereich. Bei 401/403 werden
  **Redirects unterdrückt** (Status-Code statt Redirect) — API-Verhalten.
- **Login/Logout laufen über Einmal-Ticket + echte Browser-GET** (`/client-api/auth/complete`,
  `/client-api/auth/logout-redirect`). Grund: Im **Server-Modus** (SignalR, siehe unten) läuft der
  Login-POST server-intern → `Set-Cookie` erreicht den Browser sonst nie.
- **QR-Login**: Nutzer scannt eigenen Ausweis-QR (`QrAuthCode`), optional Admin-PIN (`AdminPin`).
  `qr-identify` bestätigt nur Zugehörigkeit (für QuickActions-Menü), verleiht **keine** Rechte.
- **Passwort-Hashing**: aktuell **ungesalzenes SHA256** (`AuthenticationService.HashPassword/VerifyPassword`).
  PBKDF2-Migration ist bewusst offen (`// TODO F27`) — würde localStorage-Restore brechen.
- **Interne Self-Calls** (Server-Modus): `SelfCookieHandler` hängt Prozess-Secret `X-Fw-Internal`
  + `X-Fw-User`/`X-Fw-Role` an — **nur für den eigenen Host** (Host-Check!), Cert-Bypass nur eigener Host.
- Default-Admin für Tests: **`admin`/`admin`**.

## Dual-Mode: WASM vs. Server-Modus (iOS 12 / Alt-Geräte)

Die App rendert normal **global WebAssembly**. Für **alte iPads (iOS 12/13)**, die kein modernes
WASM/BigInt können, gibt es einen **serverseitigen Interactive-Server-Fallback** (SignalR-Circuit).
`AddInteractiveServerComponents` bleibt deshalb registriert. Konsequenzen, die man kennen muss:

- Client-Services (`AppContextService` etc.) **müssen auch in der Server-DI** registriert sein,
  sonst stirbt der Circuit beim Render → **weiße Seite** (siehe Kommentare in `Program.cs`).
- Server-gerenderte Client-Komponenten nutzen den **scoped `HttpClient`** mit `SelfCookieHandler`.
- Viele Fixes drehen sich um iOS-12-CSS (keine `clamp()`/`inset`/flex-`gap`) und Circuit-Robustheit.
- Weiche via `fw_srv`-Cookie / BigInt-Check in `App.razor`.

## Kern-Domänenmodell

Listen-Typen mit gemeinsamem Lebenszyklus `Open → Closed → Archived`:
- **AttendanceList** (Anwesenheit) + `AttendanceEntry` (mit `IsExcused` = entschuldigt)
- **OperationList** (Einsatz) + `OperationEntry` + `OperationEntryFunction`; dazu **OperationReport**
  (Einsatzbericht) mit ExternalForces/Mittel/VehicleStrength + Unterschriften
- **FireSafetyWatch** (Brandsicherheitswacht) + Requirement + Entry
- **Defect** (Mangel/Mängelliste) + `DefectStatusChange` (Verlauf)
- Stammdaten: **Member** (+ `MemberUnit` = Mehrfach-Einheiten 1..9), **Vehicle**, **User**,
  **OperationFunctionDef** (mit `StrengthPosition`: Zugführer/Gruppenführer/Mannschaft — zählt in
  Einsatzstärke), **Keyword** (+ `PersonalRequirement` je Stichwort), **ApiKey**, **ScheduledList**
  (geplante Listen, `ScheduledListBackgroundService` überführt sie automatisch), **AppSetting**
- Enums in `Models/Enums.cs`. Settings über `SettingsService` (gecacht) + `SettingKeys`
  (Modul-Sichtbarkeit `Visibility*`, Branding, Unit-Labels).

## Migrationen

`Migrations/Migration_0XX_*.cs`, FluentMigrator, fortlaufend nummeriert (aktuell bis **024**).
Neue Migration → nächste freie Nummer, Klasse mit `[Migration(n)]`. **Nie** bestehende ändern.
(Kuriosum: `Migration_010` liegt unter `FeuerwehrListen/FeuerwehrListen/Migrations/`, der Rest unter
`FeuerwehrListen/Migrations/` — beide werden vom Assembly-Scan erfasst.)

## Build / Test / Run

```bash
dotnet build FeuerwehrListen.sln          # Build (Ziel: 0 Fehler)
dotnet test                                # Playwright-E2E (startet echten Server, Chromium)
dotnet run --project FeuerwehrListen        # lokal, http://localhost:5000 (siehe run-dev.sh / launch.json)
docker compose up --build -d                # Produktion (Port 8080, Override via docker-compose.override.yml)
```
DB-Wahl über `DatabaseSettings:Provider` (SQLite|MySQL) bzw. Env `DATABASE_CONNECTION_STRING`.

## Konventionen & Fallstricke

- **Deutsch** in UI, Kommentaren, Commits. Umlaute im Code oft als ae/oe/ue geschrieben.
- Repos: eine Klasse pro Entity in `Repositories/`, als `Scoped` in DI registriert.
- Neue `/client-api`-Endpoints in `Program.cs` an passender Gruppe (`admin`/`listMgmt`/anonym) ergänzen.
- **Kiosk-Kompromisse bewusst**: manche Endpoints anonym (Eintragen/Melden/PDF für
  Anwesenheit/Einsatz/Bericht) — für LAN gewollt; Mitglieder-/Statistik-PDF bleiben Admin. Bei
  öffentlicher Erreichbarkeit absichern (DSGVO). Siehe `TODO.md` #18.
- **PDF-Inline-Endpoints** (`/client-api/export/*/pdf`) liefern ohne Token/`await` — bewahrt die
  iOS-User-Geste (sonst Download blockiert).
- Trimming im Client **aus** (`PublishTrimmed=false`) — Trimmer entfernte DTO-Record-Member →
  leere JSON-Deserialisierung.

## Statusdokumente (Kontext lesen bei Bedarf)

- **`TODO.md`** — Code-Review 2026-07-06, priorisierte Befunde (viel erledigt, einige bewusst offen)
- **`WASM-REGRESSIONS.md`** — Audit WASM-Migration vs. Server-Original (2026-07-07, weitgehend behoben)
- **`API-DOCUMENTATION.md`** — externe `/api`-REST-API
- **`README.md`** — Docker-Deployment/Ubuntu-Setup

## Git / Workflow

- Produktions-Branch: **`master`** (Auto-Deploy). Aktueller Arbeits-Branch:
  `claude/projekt-ueberblick-memory-fpmjy8`.
- Commit-Stil: `feat:` / `fix:` + knappe deutsche Beschreibung (siehe `git log`).
- Kein PR ohne explizite Aufforderung.
