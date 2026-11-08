# Versionsnummer

Die App-Version steht an **einer** Stelle: `Directory.Build.props` (`<Version>`), gilt für Server
und Client. Angezeigt wird sie im Mitgliederbereich unter *Mein Profil* (unter „Abmelden“) und
geliefert über `/client-api/app-context` (`version`).

## Schema: MAJOR.MINOR.PATCH

| Teil | Erhöhen bei | Beispiel |
|---|---|---|
| **MAJOR** | großer Umbau oder Änderung, die beim Ausrollen Handarbeit/Umstellung braucht | neues Design/WASM (2.0), Login-Pflicht + Mitgliederbereich (3.0) |
| **MINOR** | neue Funktion, neue Seite, neue Migration ohne Handarbeit | Abwesenheiten, Gerätekonten |
| **PATCH** | Fehlerbehebung, Text/Design-Korrektur ohne neue Funktion | Unterschriften-Fix |

Beim Erhöhen von MAJOR/MINOR wird der rechte Teil auf 0 gesetzt.

## Regel

* **Jeder Merge nach `master` erhöht die Version** – im selben Merge/Commit, passend zur größten
  Änderung darin (Feature → MINOR, nur Fixes → PATCH).
* Commit-Präfixe helfen bei der Wahl: `feat:` → MINOR, `fix:`/`design:` → PATCH,
  großer Umbau mit Hinweisen zum Ausrollen → MAJOR.
* Kein Tag/Branch-Zwang; die Nummer muss nur vor dem Push nach `master` stimmen, damit man am
  Gerät sieht, ob das Auto-Update (`auto-update.sh`, alle 5 Minuten) schon durch ist.

## Verlauf

* **1.x** – erste Version (Server-Blazor), ab Oktober 2025
* **2.x** – Design „Tagwache“, WebAssembly-Client, Kalender, Wachen-Erweiterungen (Juli 2026)
* **3.0.0** – Login-Pflicht mit IP-Whitelist, SSO-Provider, Mitgliederbereich mit Dienstausweis,
  Führungskreis, Gerätekonten, Login-Bremse, Einsätze zusammenlegen, Benutzer-Mitglied-
  Verknüpfung, Abwesenheiten im Voraus (November 2026)
* **3.1.0** – Eintragen in Anwesenheits-, Einsatz- und Wachenlisten mit dem Dienstausweis-QR
* **3.2.0** – Führungskreis: Protokollführung (im Kalender wählbar), Punkte ablehnen, Tagesordnung
  versenden, Aufgaben an alle Mitglieder mit Notizen/Status im Mitgliedsbereich, Aufgabe → Tagespunkt,
  Protokollversand an die Anwesenden
* **3.2.1** – Abwesenheiten landen nur auf der Liste der eigenen Einheit
* **3.2.2** – Mitgliederbereich: Tab-Leiste fest unten (nicht hinter Android-Navigation), kein Scrollen ohne Inhalt, nächste 4 Termine
