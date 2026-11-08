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
* **3.2.3** – Mitgliederbereich: kein seitliches/leeres Scrollen, keine graue Fläche unter dem Inhalt
* **3.3.0** – Protokoll: Abschnitte „Informationen“ (vorab) und „Aus den Bereichen“ (danach), Admin kann Protokolle endgültig löschen
* **3.3.1** – Mängelliste mobil: Karten statt Tabelle, Filterleiste, Detail als Vollbild
* **3.3.2** – Mängelliste zeigt standardmäßig nur offene und in Arbeit befindliche Mängel
* **3.4.0** – Mängel mit Fotos (Nextcloud, <Basis Mängel>/JJJJ/<MangelID>), eigener Basisordner in den Einstellungen
* **3.4.1** – Fix: Fotos beim Melden eines Mangels landen im richtigen Ordner
* **3.5.0** – Führungskreis: Themenbereiche und Bereiche über Sitzungen hinweg, Infos aus dem kleinen in den großen Kreis weitergeben
* **3.6.0** – Push-Benachrichtigungen (Mangel bearbeitet, neue Aufgabe, 1 h vor Dienstabend), „Meine gemeldeten Mängel“ im Mitgliederbereich
* **3.7.0** – Mängel: Notizen ohne Statusänderung (mit Push an die meldende Person)
* **3.8.0** – Mitgliederverwaltung: Lichtbild im Dialog „Zugang“ setzen (Admin/Benutzer)
* **3.8.1** – Bereich „Mängel“ für alle Mitglieder freigegeben (Migration 43), Standard für neue Mitglieder
* **3.9.0** – Kalender für alle Mitglieder freigegeben (Migration 44); einfache Mitglieder können nur noch ansehen (Ausnahme: Führungskreis-Sitzungen)
* **3.10.0** – Statistiken auch für Benutzer (nicht nur Admins)
* **3.10.1** – Statistik: Standardfilter „Gesamt“ (Anwesenheit war bei „Einsatzlisten“ immer 0), Entschuldigte zählen nicht als Anwesenheit
* **3.11.0** – Statistik: Teilnahme je Mitglied und Stichwort (sortierbare Matrix)
* **3.12.0** – Statistik: alle Mitglieder sichtbar, Mitglied anklickbar mit Einsätzen, Stichworten und Dienstabend-Teilnahme
* **3.12.1** – Kalender: mit Benutzer/Admin verknüpfte Mitglieder sehen Termine aller Einheiten
* **3.12.2** – Logo und PWA-Icon liegen im Datenordner und überleben Updates; manifest.json wird dynamisch erzeugt
