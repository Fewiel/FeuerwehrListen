# Feuerwehr Tag Helper

Kleiner **lokaler** Dienst, der Feuerwehr-Tags (`body.stl` + `inlay.stl`) mit dem
**lokal installierten OpenSCAD** auf dem PC des Admins rendert. Die FeuerwehrListen-Web-App
ruft ihn im Browser unter `http://localhost:47800` auf.

**Warum:** Das Rendern (OpenSCAD/CGAL) ist zu schwer für die kleine Server-VM. Es läuft
deshalb client-seitig — der Server wird nie mit Rendering belastet.

## Voraussetzungen (einmalig pro PC)

1. **OpenSCAD installieren:** https://openscad.org/downloads.html
   (wird automatisch unter `C:\Program Files\OpenSCAD\` gefunden).
2. **Helfer holen:** `fwtag-helper.exe` + den Ordner `scad\` (siehe „Bauen" unten).
   Die `.exe` ist eigenständig — keine .NET-Installation nötig.

## Benutzen

1. `fwtag-helper.exe` per Doppelklick starten. Ein Fenster zeigt:
   `Feuerwehr Tag Helper laeuft — URL: http://localhost:47800`.
   Das Fenster **offen lassen**, solange Tags erzeugt werden.
2. In der Web-App: **Admin → Mitglieder → Tag** bei einem Mitglied.
   Der Status „Lokaler Renderer verbunden" erscheint. **ZIP herunterladen** klicken.
3. Fertig — `feuerwehr_tag_<Nr>.zip` (body + inlay STL) landet im Download-Ordner.

Anderen Port nutzen: `fwtag-helper.exe 47900` (dann in der App die URL anpassen).

## Eigene SCAD-Vorlage / eigener OpenSCAD-Pfad

In der Web-App (Tag-Dialog → „Einstellungen") lassen sich pro Browser speichern:
- **OpenSCAD-Pfad** (falls nicht automatisch gefunden), z. B. `C:\Program Files\OpenSCAD\openscad.com`
- **SCAD-Datei** (eigene `feuerwehr_tag.scad`), z. B. aus dem FeuerwehrTag-Repo.
  Ohne Angabe wird die mitgelieferte Vorlage neben der `.exe` genutzt.

## Bauen (für Verteiler / Phillip)

```powershell
cd tools/fwtag-helper
./publish.ps1
```

Ergebnis in `tools/fwtag-helper/publish/` — diesen Ordner (`fwtag-helper.exe` + `scad\`)
an die Admins verteilen.

Zum Testen ohne Publish: `dotnet run` (im Ordner `tools/fwtag-helper`).

## Sicherheit

Der Dienst bindet nur an `localhost` (127.0.0.1) — von außen nicht erreichbar. Er sollte
nur laufen, während Tags erzeugt werden.
