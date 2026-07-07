# WASM-Migrations-Regressionen (Audit 2026-07-07)

## STATUS 2026-07-07 — behoben auf Branch fix/wasm-regressions (Build 0 Fehler)

Behoben (Backend-Agent + 7 Frontend-Agents): **alle HOCH (#1-5)**, **Sicherheit #6**,
**alle MITTEL (#7-18)**, Benutzer-Funktionen + QR-Schnellaktionen, sowie **alle NIEDRIG-Punkte**.
Details je Seite siehe Commit. **Einzig offen geblieben:** die frühere „QR-Tag-Nachbestellung
per Mail" (Numpad-Aktion) — braucht einen eigenen Mail-Endpoint + Numpad-Flow, bewusst
zurückgestellt (Nischenfunktion). Alles andere ist wiederhergestellt.

---


Vergleich jeder migrierten WASM-Seite gegen ihr server-gerendertes Original aus der
Git-History. Nur echte Funktionsverluste; Architektur/Styling ignoriert.
Statistik-Seite bereits separat wiederhergestellt (Commit 409bbe9).

## HOCH — echte Funktions-/Datenverluste

1. **Funktionen: „Zählstelle in der Stärke" (StrengthPosition) komplett verloren.**
   `Admin/OperationFunctions.razor` + Backend `Program.cs` (/functions GET/POST/PUT).
   Dropdown (Zugführer/Gruppenführer/Mannschaft), Tabellenspalte, DTO und Backend fehlen.
   **Datenverlust:** PUT erzeugt neues `OperationFunctionDef` ohne StrengthPosition → jedes
   Bearbeiten setzt bestehende Zuordnung still auf Default „Mannschaft" zurück. Feld steuert,
   wo eine Funktion in der Einsatzstärke (F/M/Gesamt) gezählt wird → wirkt in Einsatzbericht.

2. **Einstellungen: Logo-Upload entfernt.** `Admin/Settings.razor` + Backend.
   InputFile + Vorschau + Entfernen weg; kein Upload-Endpoint. Admin kann kein App-Logo setzen.

3. **Einstellungen: PWA-Icon-Upload entfernt.** `Admin/Settings.razor` + Backend.
   Icon-Upload (schrieb icon-192/512 + manifest) weg; kein Endpoint. PWA-Icon nicht änderbar.

4. **Einstellungen: Nextcloud „Verbindung testen" entfernt.** `Admin/Settings.razor` + Backend.
   Test-Button + Rückmeldung weg; kein Test-Endpoint. Zugangsdaten nicht mehr verifizierbar.

5. **Geplante Listen: „Jetzt öffnen" (ProcessNow) verloren.** `Admin/ScheduledLists.razor` + Backend.
   Button zum sofortigen manuellen Überführen einer geplanten in eine echte Liste weg;
   kein Backend-Endpoint (nur GET/POST/DELETE). Nur „Löschen" übrig.

## SICHERHEIT / RECHTE (Regression, bestätigt Review-Finding #19)

6. **Listen löschen/archivieren nicht mehr Admin-beschränkt.** `Admin/Archive.razor`,
   `Admin/ClosedLists.razor`; Backend `DELETE|POST /client-api/list/{type}/{id}` nur
   `RequireAuthorization()` (nicht Admin). Im Original war Löschen Admin-only → jetzt kann jeder
   angemeldete Nutzer Listen (auch aus dem Archiv, endgültig) löschen/archivieren.

## MITTEL — spürbare Verluste

7. **Einsatzbericht: Einsatz-Kopfdaten fehlen.** `OperationReportPage.razor` + Backend-Endpoint
   `/client-api/operation/{id}/report`. Nr./Alarmstichwort/Alarmzeit werden nicht geliefert/gezeigt
   → man sieht nicht, für welchen Einsatz der Bericht ist.
8. **Einsatzbericht: Gesamtstärke nicht mehr live.** Statischer String vom Laden; Änderungen an
   externen Kräften/Fahrzeug-Stärken aktualisieren das Stärke-Badge erst nach Speichern+Reload.
9. **Einstellungen: Nextcloud-Standardordner-Default verloren** (`/Feuerwehr Billerbeck/Einsatzbilder`).
10. **Einstellungen: Erklärende Hilfetexte weg** (Nextcloud-App-Passwort-Anleitung, Zielordner-Schema,
    QR-Nachbestellung-Erklärung) — Bedienlogik, nicht nur Wording.
11. **Einsatz-Detail: Personal-Requirements-Status-Anzeige fehlt.** `OperationDetail.razor` + Backend
    liefert die Daten nicht (fehlende Funktionen bei Stichwort mit Requirements).
12. **Geplante Listen: „FÄLLIG!"-Badge fehlt** für überfällige unverarbeitete Listen.
13. **Wachen-Liste: Admin-Button „Neue Wache anlegen" fehlt.** `FireSafetyWatchList.razor` — Create
    nur noch per direkter URL erreichbar.
14. **Wachen-Detail: QR-User-Ausweis → QuickActions entfällt** (siehe auch systemweit).
15. **Benutzer: E-Mail-Feld + Willkommens-Mail entfernt.** `Admin/Users.razor` + Backend speichert kein
    Email mehr; kein automatischer Zugangsdaten-Versand.
16. **Benutzer: QR-Code-Scanner (Kamera) im Formular entfernt** — nur noch manuelle Code-Eingabe.
17. **Benutzer: Auto-Passwort-Generierung + einmalige Anzeige/Kopieren entfernt** — manuelle Eingabe.
18. **Mängelliste: Mitglieder-Namenssuche (Autocomplete) entfernt** — beim Melden UND Statuswechsel;
    nur noch manuelle Mitgliedsnummer.

## SYSTEMWEIT (architektonisch, mehrere Seiten)

- **QR-Login von Nutzern → QuickActionsModal** (eigenen Ausweis scannen → Schnellaktionen wie
  „Liste schließen") entfällt auf AttendanceDetail, OperationDetail, FireSafetyWatchDetail.
- **QR-Tag-Nachbestellung per Mail** (frühere Numpad-Aktion) entfällt.

## NIEDRIG — kosmetisch / Info / Verhalten

- Mitglieder: Einheiten als Nummer statt Label; CSV-Import-Fehler nur als Anzahl statt Zeilen.
- Einsatz-Detail: Eintrags-Löschen auf Detailseite verlagert (nur noch „Einträge bearbeiten");
  Alarmzeit/Erstellt-Datum im Kopf fehlen.
- Einsatz-Einträge bearbeiten: „Eingetragen"-Uhrzeitspalte fehlt.
- Einsatzbericht: Fahrzeug-Stärken-Modal ohne Sofort-Speichern; Header-Zurück-Button.
- Service-Status: Serverzeit-Karte (WASM kann keine Serverzeit rendern).
- Geplante Listen: „Löschen" jetzt für alle Zeilen statt nur unverarbeitete.
- Fahrzeuge: „Erstellt am"-Spalte, Langtyp-Name in der Liste.
- Mängelliste: Fahrzeug-Pflicht gelockert; Statusoptionen nicht kontextabhängig; „Erledigt am/durch"
  + „Von-Status" im Detail/Verlauf nicht angezeigt.
- Wachen-Detail: QR-Requirement-Auswahl-Modal entfällt (Scan braucht vorher gewählte Anforderung).
- Wache anlegen: Anforderungszeilen ohne Funktion werden still verworfen.

## PARITÄT OK (kein Verlust)
Startseite, Stichwörter+Requirements, Anwesenheits-Detail, Anwesenheitsliste anlegen,
Einsatzliste anlegen, Login, Logout, API-Keys, Abgeschlossene Listen, Passwort ändern.
(Members, OperationEditEntries, Archive, Vehicles, ServiceStatus: nur NIEDRIG/oben genannt.)
