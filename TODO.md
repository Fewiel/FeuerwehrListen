# TODO — Code-Review 2026-07-06

## Status 2026-07-06 — Branch `fix/code-review-2026-07` (Build: 0 Fehler)

Umgesetzt via Multi-Agent-Fix (Opus 4.8). **Erledigt:** #1 (/token abgesichert), #2 (Login/Logout
im Server-Modus via Ticket+GET-Redirect repariert), #3 (SelfCookieHandler Host-Check, Cert-Bypass
nur eigener Host, BaseUrl aus Config), #4 (Tag-Helper nur noch im WASM-Browser), #5, #6, #7, #8, #9,
#10, #11 (echte Icons 192/512/180), #12, #14, #16, #17, #19 (Members/ScheduledLists; Archive/ClosedLists
bewusst nicht — deren API verlangt nur Auth, nicht Admin), #20, #21, #22, #23, #24, #25, #26, #28,
#29, #30, #32, #34, #36, #37, #38. **Plus neuer Befund: PDF-Download auf neuem iPad (WASM)** — behoben
durch cookie-authentifizierte Inline-PDF-Endpoints ohne Token-`await` (User-Geste bleibt erhalten).

**Teilweise / offen:**
- #13 (Bild-Upload Server-Modus): nur defensiver UI-Hinweis; echter JS-`fetch`-Upload (umgeht Circuit) noch offen.
- #27 (Passwort-Hashing): `VerifyPassword` abwärtskompatibel eingeführt + alle Verify-Stellen umgestellt;
  PBKDF2-Default + Re-Hashing NICHT umgesetzt (würde localStorage-Restore brechen) — `// TODO F27` gesetzt.
- #33 (returnUrl): Parameter wird gesetzt, aber `auth/complete` redirectet hart auf `/` — echte Rückkehr
  bräuchte Server-Änderung; bewusst minimal gelassen.
- #15 (Server-seitiger Unique-Index gegen Doppel-Einträge): Client-Busy-Flags gesetzt; DB-Constraint offen.
- #18 (anonyme Endpoints): nur kommentiert (bewusst offen für Kiosk-LAN).
- appsettings `AllowedHosts` bleibt `*` (kommentiert); `AppSettings:BaseUrl` für Produktion setzen.

---

# TODO — Code-Review 2026-07-06 (Original-Befunde)

Ergebnis eines vollständigen Reviews (Client-Pages, Client-Komponenten/Services, Backend/API,
statische Assets, Parität WASM ↔ Server-Modus/SignalR). Sortiert nach Priorität.

## KRITISCH

- [ ] **1. `/token`-Endpoint absichern** — `FeuerwehrListen/Program.cs:273`
  `GET /token?path=...` ist anonym und stellt Download-Tokens für **beliebige** Pfade aus.
  Damit kann jeder ohne Anmeldung z. B. `/api/export/members/pdf` (komplette Mitgliederliste)
  oder alle Listen-/Bericht-PDFs laden — der gesamte Export-Schutz ist wirkungslos.
  Fix: `.RequireAuthorization()` + serverseitige Pfad-Whitelist (`/api/export/…`).

- [ ] **2. Login/Logout im Server-Modus (SignalR, iOS 12) ist kaputt** — `Login.razor:103`, `Logout.razor:13`, `Program.cs:362-388`, `Services/SelfHttpClient.cs`
  Im Server-Modus läuft `POST client-api/auth/login` **server-intern** (SelfCookieHandler);
  das `Set-Cookie: fw_auth` erreicht den Browser nie. Nach `forceLoad` ist der Nutzer wieder anonym.
  Folge: Auf iOS-12-Geräten ist der **gesamte angemeldete Bereich unerreichbar**
  (Admin, Einsatzbericht, Archiv, geschlossene Listen, Einträge bearbeiten). Logout löscht den
  Cookie analog nicht (Gerät bleibt ggf. dauerhaft angemeldet — Gemeinschaftsgeräte!).
  Fix: Login über echten Browser-Request abschließen: Endpoint gibt One-Time-Token zurück,
  Client navigiert mit `forceLoad` auf `GET /client-api/auth/cookie?token=…` → dort `SignInAsync`
  + Redirect. Logout analog (`GET …/logout-redirect`). Funktioniert in beiden Modi identisch.

- [ ] **3. Internes Auth-Secret leakt an fremde Hosts** — `Services/SelfHttpClient.cs:45-71`, `Program.cs:172-192, 233-264`, `Admin/Members.razor:253-292`
  `SelfCookieHandler` hängt `X-Fw-Internal` (Prozess-Secret, erlaubt Admin-Impersonation) bzw. den
  Auth-Cookie an **jeden** Request des injizierten HttpClient — ohne Host-Prüfung. `Members.razor`
  ruft mit demselben Client die frei konfigurierbare `_helperUrl` (localStorage, http!) auf.
  Zusätzlich: `AppBaseUrlProvider` übernimmt den **Host-Header des ersten Requests** (Poisoning),
  und `DangerousAcceptAnyServerCertificateValidator` gilt für alle Ziele.
  Fix: Header/Cookie nur setzen, wenn `RequestUri.Host` == eigene BaseAddress; `AllowedHosts`
  konfigurieren; Basis-URL aus Konfiguration statt Host-Header; Cert-Bypass auf eigenen Host beschränken.

## HOCH

- [ ] **4. 3D-Tag-Helper im Server-Modus prinzipiell kaputt (+ SSRF)** — `Admin/Members.razor:253-292`
  `Http.GetAsync("http://localhost:47800/…")` läuft im Server-Modus **auf dem Server**, nicht auf dem
  Admin-PC → Helper immer "down", Download unmöglich; zudem SSRF auf frei eingebbare URL (siehe #3).
  Fix: Ping/Download per JS-Interop-`fetch` im Browser (modusunabhängig, braucht CORS im Helper)
  oder Feature im Server-Modus mit Hinweis ausblenden.

- [ ] **5. Fehlende `catch`-Blöcke in Admin-Save/Delete → App-Crash bei Netzfehler**
  `Users.razor:83`, `Members.razor:308+319`, `Vehicles.razor:78+89`, `KeywordsManagement.razor:99+109`,
  `OperationFunctions.razor:68+79`, `ScheduledLists.razor:95+105`, `ApiKeys.razor:69+81`,
  `Settings.razor:102`, `CreateFireSafetyWatch.razor:65`, `FireSafetyWatchDetail.razor:104`,
  `Archive.razor:55`, `ClosedLists.razor:68`.
  `try/finally` ohne `catch` bzw. gar kein try: Netzfehler → gelbes Blazor-Fehlerbanner; im
  Server-Modus stirbt der Circuit ("Verbindung unterbrochen").
  Fix: überall `catch` + Fehlermeldung; Muster aus `CreateAttendance`/`Login.DoLogin` übernehmen.

- [ ] **6. Destruktive Aktionen ohne Bestätigung** — `Admin/Archive.razor:55` (endgültiges Löschen!),
  `Users/Members/Vehicles/ApiKeys/Keywords/Functions/ScheduledLists`-Delete.
  Ein Fehlklick aufs Mülleimer-Icon löscht unwiderruflich. Fix: Bestätigungs-Modal bzw. `confirm()`.

- [ ] **7. QR-Scanner: "Kein Kamerazugriff" ist toter Code** — `wwwroot/qr-scanner.js:134-137`, `Components/QrScanner.razor:74-89`
  JS-`start()` schluckt alle Fehler (Kamera verweigert, kein BarcodeDetector) und resolved normal →
  `_noCameraAccess` wird nie gesetzt, Nutzer sieht nur ein schwarzes Rechteck.
  Fix: `start()` gibt `bool` zurück (bzw. rethrowt), Komponente wertet aus.

- [ ] **8. CookieAuthStateProvider: Netzfehler = "abgemeldet" + keine Cache-Nutzung** — `Client/Services/CookieAuthStateProvider.cs:16-33`, `Components/AuthGuard.razor:29-41`
  Transienter Fehler bei `/auth/me` → anonymer Principal → AuthGuard wirft den Nutzer zum Login,
  obwohl der Cookie gültig ist. Zudem 2-3 parallele `/auth/me`-Calls beim Start + 1 pro Navigation.
  Fix: Ergebnis cachen (`Task`-Memoisierung), bei Exception letzten bekannten Status behalten;
  AuthGuard auf `[CascadingParameter] Task<AuthenticationState>` umstellen.

- [ ] **9. Detailseiten hängen bei Fehler dauerhaft in "Lade…"** — `AttendanceDetail.razor:104`,
  `OperationDetail.razor:179`, `OperationEditEntries.razor:47`, `OperationReportPage.razor:291`,
  `FireSafetyWatchDetail.razor:81`, `Archive.razor:45`, `ClosedLists.razor:55`
  `catch { _list = null; }` → ewiges "Lade…" bei 404/Netzfehler. Besonders kritisch: nach jedem
  Eintrag wird `Load()` erneut ausgeführt — ein transienter Fehler wirft die funktionierende
  Anzeige mitten im Betrieb weg. Fix: getrennter Fehlerzustand + "Erneut versuchen", alte Daten behalten.

- [ ] **10. app.css bricht auf genau den iOS-12/13-Geräten des Server-Modus** — `wwwroot/app.css`
  `clamp()` (Safari ≥13.1), `inset` (≥14.5), Flex-`gap` (≥14.1), `aspect-ratio` (≥15) → Topbar-Padding 0,
  Overlays ohne Positionierung, Nav klebt zusammen. Außerdem `app.css:341`: `@@keyframes` (Razor-Escape
  in statischer CSS-Datei!) → Animation `confirmation-in` existiert nicht.
  Fix: Fallbacks voranstellen (`top/right/bottom/left` vor `inset`, festes Padding vor `clamp`,
  Margins statt `gap` in der Topbar); `@@keyframes` → `@keyframes`; mit `?rmode=server` auf Alt-Gerät testen.

- [ ] **11. PWA-Icons: 2× 1,7 MB, falsche Größen; apple-touch-icon = 32 px** — `wwwroot/icon-192.png`, `icon-512.png`, `Components/App.razor:10`
  Beide Icons sind identische 1024×1024/1,7-MB-Dateien (manifest deklariert 192/512); iOS nutzt ohnehin
  nur `apple-touch-icon`, das auf das 32-px-Favicon zeigt → matschiges Homescreen-Icon auf den iPads.
  Fix: echte 192/512-PNGs (optimiert) + 180×180-`apple-touch-icon`.

- [ ] **12. Timer-Leak bei schneller Navigation** — `Home.razor:248-254`, `Admin/ServiceStatus.razor:58-64`
  Timer wird nach `await` erzeugt; navigiert man vorher weg, läuft `Dispose()` vor der Erzeugung →
  Timer pollt für immer weiter (30 s bzw. 10 s), inkl. StateHasChanged auf toter Komponente.
  Fix: `_disposed`-Flag prüfen bzw. Timer vor dem ersten `await` anlegen.

- [ ] **13. Bild-Upload Einsatzbericht im Server-Modus riskant** — `OperationReportPage.razor:328-342`
  Bis 50×20 MB laufen über den Long-Polling-Circuit (Browser→Server) und danach per Multipart
  **nochmal** an den eigenen Endpoint. Im Einsatznetz praktisch sicher Circuit-Timeout; Abbruch
  reißt die ganze Seite mit. Fix: Upload per JS-Interop-`fetch` (FormData direkt vom `<input>`).

## MITTEL

- [ ] **14. Nextcloud-Bildzähler wird nie geladen** — `OperationReportPage.razor:293-299`
  `OnAfterRenderAsync(first)` prüft `_b`, das beim ersten Render immer noch `null` ist → Badge zeigt
  dauerhaft "Noch keine Bilder". Fix: Abruf ans Ende von `Load()` verschieben.

- [ ] **15. Doppel-Submit-Races** — `AttendanceDetail.razor:134` (`Post`/`Excuse`),
  `FireSafetyWatchDetail.razor:85`, `DefectList.razor:136`, `Login.razor:132` (`SubmitPin`)
  Kein Busy-Flag → Doppelklick erzeugt doppelte Einträge (Server-Duplikatprüfung ist check-then-insert,
  nicht atomar). Fix: Busy-Flag + `disabled` (Muster: `OperationDetail.Submit`).
  Serverseitig zusätzlich: Unique-Index bzw. echte `MemberId`-Spalte statt `NameOrId`-String-Matching
  (`Program.cs:769`, `:922`, `:1031`).

- [ ] **16. `operation/{id}/add` prüft Listenstatus nicht** — `Program.cs:915-943`
  Anders als bei Attendance kann anonym in **geschlossene** Einsatzlisten eingetragen werden.
  Fix: Status prüfen (geschlossen nur für Angemeldete), konsistent zu `attendance/{id}/add`.

- [ ] **17. Mangel-Status anonym änderbar per beliebiger Mitgliedsnummer** — `Program.cs:982-994`
  Mitgliedsnummern sind kein Auth-Merkmal (über `/resolve` enumerierbar). Fix: an Anmeldung/Rolle binden.

- [ ] **18. Bewusste Entscheidung dokumentieren: anonyme Endpoints** — `Program.cs:1126` (attendance/create),
  `:1142` (operation/create, legt auch Keywords an), `:891` (`/resolve` → Mitglieder-Enumeration mit
  Klarnamen), `/client-api/feedback` (Mail-Trigger).
  Im LAN-Kiosk-Kontext teils gewollt — falls die App je öffentlich erreichbar ist: absichern (DSGVO!).
  Mindestens: Listen-Anlegen hinter Auth, `/resolve` rate-limiten.

- [ ] **19. AuthGuard-Inkonsistenz: `RequireAdmin` fehlt** — `Admin/Members.razor:28`,
  `ScheduledLists.razor:9`, `Archive.razor:9`, `ClosedLists.razor:9`
  Normale Nutzer sehen die Seite, alle API-Calls laufen in 401 → wirkt wie "keine Daten".
  Fix: `RequireAdmin="true"` überall, wo die API Admin verlangt.

- [ ] **20. Save schließt Formular auch bei Server-Fehler, ohne Meldung** — `Members.razor:312`,
  `Vehicles.razor:81`, `KeywordsManagement.razor:103`, `OperationFunctions.razor:72`,
  `Settings.razor:103` ("Gespeichert." auch bei 401/500), `OperationEditEntries.razor:49`
  (meldet "gelöscht" auch bei 401). Fix: `IsSuccessStatusCode` prüfen, Formular offen lassen, Fehler anzeigen.

- [ ] **21. Prerender-Doppel-Laden** — `App.razor:40,126` + alle Pages
  Jede Seite lädt Daten doppelt (SSR + interaktiv); im Server-Modus inkl. doppelter Self-HTTP-Calls
  und Flackern. Fix: `prerender: false` in der `Mode`-Property oder `PersistentComponentState`.

- [ ] **22. Alt-Geräte-Weiche härten** — `App.razor:26-33, 135`
  (a) Cookies blockiert → Endlos-Reload-Schleife (Guard via sessionStorage/URL-Marker + statische Meldung).
  (b) `fw_srv`-Cookie (1 Jahr) klebt nach iOS-Update → Gerät bleibt unnötig im Server-Modus
  (Umkehr-Check: wenn BigInt ok und Cookie gesetzt → Cookie löschen).

- [ ] **23. legacy.html löschen** — `wwwroot/legacy.html`
  Nirgends mehr verlinkt (Server-Modus hat sie ersetzt), veraltet still gegen die API, fehlende
  Features (Wachen, Mängel, Login, …). Fix: löschen + Redirect `/legacy.html` → `/` für gepinnte
  Homescreen-Verknüpfungen.

- [ ] **24. Error.razor rendert im Fehlerfall vermutlich leer** — `Components/Pages/Error.razor`, `Client/Routes.razor:2`
  `UseExceptionHandler("/Error")` zeigt auf eine Route der Server-Assembly, die der Client-Router
  nicht kennt (kein `AdditionalAssemblies`, kein `<NotFound>`); zudem englisches Standard-Template.
  Fix: statische deutsche Fehlerseite oder Router-Assemblies ergänzen; einmal mit Test-Exception prüfen.

- [ ] **25. manifest.json-Schreiben per Regex** — `Program.cs:648-669`
  `appName` wird uneskapiert als Regex-Replacement eingesetzt (`$`, `"` korrumpieren die Datei);
  kein Locking; read-only-Container scheitert still. Fix: JSON-DOM statt Regex, Fehler loggen.

- [ ] **26. SignaturePad: Listener-Leak + keine Retina-Auflösung** — `wwwroot/signature.js:51`, `Components/SignaturePad.razor`
  `window.addEventListener("mouseup")` wird nie entfernt (relevant im langlebigen Server-Circuit);
  Canvas ohne `devicePixelRatio`-Skalierung → pixelige Unterschriften. Fix: `detach()` + `DisposeAsync`; dpr-Backing-Store.

- [ ] **27. Passwort-Hashing modernisieren** — `Services/AuthenticationService.cs:248`, `Program.cs:365`
  Ungesalzenes SHA256; der Hash in `ProtectedLocalStorage` ist ein vollwertiges Login-Credential.
  Fix: `PasswordHasher<T>`/PBKDF2 + serverseitig widerrufbares Session-Token statt Hash-Speicherung.
  (Migration: beim nächsten erfolgreichen Login re-hashen.)

- [ ] **28. Barcode-Polyfill: einkompilierter CDN-Fallback** — `wwwroot/lib/barcode-detector/polyfill.min.js`
  Default-`locateFile` zeigt auf jsDelivr; der lokale Override greift, fällt aber bei künftigen
  Polyfill-Updates **stumm** aufs CDN zurück (Projekt-Regel: alles lokal!).
  Fix: URL in der Datei auf `/lib/barcode-detector/` patchen oder Override hart failen lassen.

- [ ] **29. AuthenticationService (Server-Modus): FirstName/LastName-Claims fehlen** — `Services/AuthenticationService.cs:80-87`
  `BuildAuthState()` enthält nur Name+Rolle → NavMenu zeigt im Server-Modus Initialen aus dem
  Usernamen statt Vor-/Nachname. Fix: Claims ergänzen.

## NIEDRIG

- [ ] **30.** `app-context` wird 2-4× unabhängig geladen; Default "alle Module sichtbar" flackert —
  gecachter `AppContextService` (`NavMenu.razor:146`, `Home.razor:250`, `CreateAttendance.razor:68`, `ScheduledLists.razor:85`).
- [ ] **31.** Duplizierter Code zentralisieren: ExportPdf-Muster (5×), Confirm-Overlay+Sound (3×),
  Admin-CRUD-Muster (5×) — eine gemeinsame Basis würde #5/#6/#20 an einer Stelle fixen.
- [ ] **32.** QR-Scanner-Feinschliff: später Detect-Callback nach `stop()` feuert noch ins .NET
  (`qr-scanner.js:93-96`, Generation-Token + `.catch()`); Login: Scanner während PIN-Eingabe aktiv
  (`Login.razor:42`, `IsActive="@(!_showPin)"`); Toggle-Logik `IsActive` false→true startet nie
  (`QrScanner.razor:54-72`, latent).
- [ ] **33.** AuthGuard ohne `returnUrl` — nach Login immer `/`.
- [ ] **34.** Home: Feedback-Suche ohne Cancel/Sequenz-Check (`Home.razor:272`); Confirm-Overlay:
  überlappende Hide-Tasks (`AttendanceDetail.razor:185` u. a.).
- [ ] **35.** `Settings`: nie gesetzte Zahlen-Keys werden beim Speichern zu `"0"` (`Settings.razor:91`).
- [ ] **36.** `GeocodingService`: eigener `HttpClient` pro Scoped-Instanz → `IHttpClientFactory` nutzen.
- [ ] **37.** `DownloadController`: anonym, reflektiert Client-Base64 — entfernen oder
  `Content-Disposition: attachment` + Größenlimit.
- [ ] **38.** NavMenu: doppelter Backdrop bei mobilem Menü (`NavMenu.razor:47+89`).
- [ ] **39.** DateTime: Client-Uhr vs. Server-`DateTime.Now` (Dringend-Markierung Wachen,
  Alarmzeit-Default) — Serverzeit im DTO mitliefern oder bewusst dokumentieren.
- [ ] **40.** Home-Polling-Timer läuft für bis zu 100 disconnected Circuits (6 min) weiter —
  optional via `CircuitHandler` pausieren.

## Paritäts-Fazit WASM ↔ Server-Modus (SignalR)

**Anonyme Kern-Flows sind paritätisch und sauber gebaut** (Übersicht, Eintragen per QR/manuell,
Wachen, Mängel melden, Feedback, PDF via Token-URL; JS-Interop durchgehend OnAfterRender-diszipliniert,
`InvokeAsync` korrekt). **Keine Parität** besteht bei:
1. **Login/Logout → gesamter angemeldeter Bereich** (Punkt 2) — auf iOS 12 faktisch nur die anonyme App-Hälfte nutzbar.
2. **3D-Tag-Download** (Punkt 4).
3. **Bild-Upload** funktioniert theoretisch, wird aber über Long-Polling praktisch scheitern (Punkt 13).
4. **Optik auf Alt-Geräten** durch modernes CSS beschädigt (Punkt 10).
