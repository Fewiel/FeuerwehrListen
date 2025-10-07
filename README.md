# 🚒 Feuerwehr Listen - Anwesenheits- und Einsatzlisten-Management

Eine moderne Blazor Server Webanwendung zur Verwaltung von Anwesenheits- und Einsatzlisten für Feuerwehren.

## ✨ Funktionen

### 📋 Öffentlicher Bereich (Kein Login erforderlich)

#### Übersicht
- **Auto-Refresh**: Automatische Aktualisierung alle 30 Sekunden
- **Anzeige**: Alle aktuell offenen Anwesenheits- und Einsatzlisten
- **Schnellzugriff**: Direkt zu den Listen navigieren

#### Anwesenheitslisten
- **Erstellen**: Titel, Einheit, Beschreibung (Datum/Uhrzeit automatisch)
- **Eintragen**: Mitgliedsnummer oder Name eingeben
- **Validierung**: Nur registrierte Mitglieder können eingetragen werden
- **Autofocus**: Eingabefeld erhält automatisch Focus
- **Enter-Taste**: Schnelles Eintragen per Enter
- **Entfernen**: Falsche Einträge können gelöscht werden (nur bei offenen Listen)
- **Abschließen**: Listen können geschlossen werden

#### Einsatzlisten
- **Erstellen**: Einsatznummer, Stichwort, Alarmierungszeit
- **Eintragen**: 
  - Mitgliedsnummer oder Name (validiert)
  - Fahrzeug aus Dropdown auswählen
  - Funktion: Maschinist, Gruppenführer oder Trupp
  - Atemschutz-Checkbox
- **Fahrzeug-Sortierung**: Einträge werden nach Fahrzeugen gruppiert angezeigt
- **Entfernen**: Falsche Einträge können gelöscht werden
- **Abschließen**: Listen können geschlossen werden

### 🔐 Admin-Bereich (Login erforderlich)

#### Authentifizierung
- **Login-System**: Sichere Anmeldung für Admin-Bereich
- **Standard-Admin**: `admin` / `admin` (Passwort sollte geändert werden!)
- **Passwort ändern**: Eigenes Passwort ändern
- **Logout**: Sicheres Abmelden

#### Mitgliederverwaltung
- **Mitglieder anlegen**: Mitgliedsnummer, Vor- und Nachname
- **Status**: Aktiv/Inaktiv
- **Validierung**: Eindeutige Mitgliedsnummern
- **Bearbeiten & Löschen**: Volle Verwaltung

#### Fahrzeugverwaltung
- **Fahrzeuge anlegen**: Name, Funkrufname, Typ
- **Fahrzeugtypen**: LF, TLF, DLK, RW, MTW, KdoW, Sonstige
- **Status**: Aktiv/Inaktiv
- **Bearbeiten & Löschen**: Volle Verwaltung

#### Abgeschlossene Listen & Archiv
- **Übersicht**: Alle geschlossenen Listen
- **Archivierung**: Listen können archiviert werden
- **Wiederherstellen**: Archivierte Listen einsehen

#### Benutzerverwaltung
- **Admin-Benutzer anlegen**: Mit zufällig generiertem Passwort
- **Rollen**: Benutzer oder Administrator
- **Passwort-Anzeige**: Generiertes Passwort wird einmalig angezeigt
- **Kopier-Funktion**: Passwort in Zwischenablage kopieren

#### API Keys
- **Verwaltung**: API-Schlüssel für externe Tools
- **Beschreibung**: Zweck des Keys dokumentieren
- **Status**: Aktiv/Inaktiv

#### Geplante Listen
- **Vorausplanung**: Listen im Voraus erstellen
- **Automatisches Öffnen**: X Minuten vor Event automatisch öffnen
- **Background-Service**: Prüft jede Minute auf fällige Listen
- **Manuelles Öffnen**: "Jetzt öffnen" Button für sofortiges Öffnen
- **Auto-Refresh**: Status-Aktualisierung alle 10 Sekunden

#### Service Status (Debug)
- **Background-Service Monitoring**: Echtzeit-Status
- **Fällige Listen**: Übersicht aller fälligen geplanten Listen
- **Zeitberechnung**: Prüfung der Öffnungszeiten
- **Auto-Refresh**: Aktualisierung alle 5 Sekunden

### 🎨 Design
- **Dark Mode**: Modernes dunkles Design (GitHub-inspiriert)
- **Bootstrap 5**: Responsive und modern
- **Benutzerfreundlich**: Einfache, intuitive Bedienung
- **Icons**: Emoji-Icons für schnelle Orientierung
- **🚒 Feuerwehr-Favicon**: Individuelles SVG-Favicon

## 🛠 Technologie-Stack

### Backend
- **C# .NET 8.0**
- **Blazor Server** mit InteractiveServer RenderMode
- **linq2db**: Type-safe Datenbankzugriff
- **FluentMigrator**: Automatische Datenbank-Migrationen
- **Background Services**: Für geplante Listen

### Frontend
- **Blazor Components**: Keine JavaScript-Dateien
- **Bootstrap 5**: Responsives Design
- **Custom CSS**: Dark Theme

### Datenbank
- **SQLite** (Standard - keine Installation nötig)
- **MySQL** (optional - konfigurierbar)

## 📁 Projekt-Struktur

```
FeuerwehrListen/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   ├── NavMenu.razor
│   │   └── AdminAuthCheck.razor          # Admin-Route-Schutz
│   └── Pages/
│       ├── Home.razor                     # Übersicht (Auto-Refresh)
│       ├── Login.razor                    # Login-Seite
│       ├── Logout.razor                   # Logout-Handler
│       ├── CreateAttendance.razor
│       ├── CreateOperation.razor
│       ├── AttendanceDetail.razor         # Mit Löschen-Funktion
│       ├── OperationDetail.razor          # Mit Fahrzeug-Sortierung
│       └── Admin/
│           ├── ClosedLists.razor
│           ├── Archive.razor
│           ├── Members.razor              # Mitgliederverwaltung
│           ├── Vehicles.razor             # Fahrzeugverwaltung
│           ├── Users.razor                # Mit Passwort-Generator
│           ├── ApiKeys.razor
│           ├── ScheduledLists.razor       # Mit Auto-Refresh
│           ├── ServiceStatus.razor        # Debug-Seite
│           └── ChangePassword.razor       # Passwort ändern
├── Data/
│   └── AppDbConnection.cs
├── Migrations/
│   ├── Migration_001_InitialSchema.cs
│   ├── Migration_002_AddVehicles.cs
│   └── Migration_003_AddMembers.cs
├── Models/
│   ├── AttendanceList.cs                  # Mit linq2db Mapping
│   ├── OperationList.cs                   # Mit linq2db Mapping
│   ├── AttendanceEntry.cs
│   ├── OperationEntry.cs
│   ├── Member.cs                          # Mitglieder-Model
│   ├── Vehicle.cs                         # Fahrzeug-Model
│   ├── User.cs                            # Mit Authentifizierung
│   ├── ApiKey.cs
│   ├── ScheduledList.cs
│   └── Enums.cs                           # ListStatus, UserRole, etc.
├── Repositories/
│   ├── AttendanceListRepository.cs
│   ├── OperationListRepository.cs
│   ├── AttendanceEntryRepository.cs       # Mit DeleteAsync
│   ├── OperationEntryRepository.cs        # Mit DeleteAsync
│   ├── MemberRepository.cs                # Mit FindByNameOrNumber
│   ├── VehicleRepository.cs
│   ├── UserRepository.cs
│   ├── ApiKeyRepository.cs
│   └── ScheduledListRepository.cs         # Mit GetDueAsync
├── Services/
│   ├── AuthenticationService.cs           # Login/Logout/Passwort
│   └── ScheduledListBackgroundService.cs  # Auto-Öffnen von Listen
└── wwwroot/
    ├── app.css                            # Dark Theme
    ├── favicon.svg                        # Feuerwehr-Icon
    └── bootstrap/                         # Bootstrap 5
```

## 🚀 Installation

### Voraussetzungen
- **.NET 8.0 SDK** ([Download](https://dotnet.microsoft.com/download))
- Optional: **MySQL Server** (falls MySQL statt SQLite)

### Schritte

1. **Repository klonen**:
```bash
git clone <repository-url>
cd FeuerwehrListen
```

2. **NuGet-Pakete wiederherstellen**:
```bash
dotnet restore
```

3. **Datenbank konfigurieren** in `appsettings.json`:
```json
{
  "DatabaseSettings": {
    "Provider": "SQLite",
    "SQLiteConnection": "Data Source=feuerwehr.db",
    "MySQLConnection": "Server=localhost;Database=feuerwehr;User=root;Password=password;"
  }
}
```

Für MySQL: `Provider` auf `"MySQL"` ändern und Connection-String anpassen.

4. **Anwendung starten**:
```bash
cd FeuerwehrListen
dotnet run
```

Die Anwendung ist dann unter `http://localhost:5161` erreichbar.

**Wichtig**: Die Datenbank-Migrationen werden beim ersten Start automatisch ausgeführt!

### Erster Start

Nach dem ersten Start:
1. **Mitglieder anlegen**: Admin-Bereich → Mitglieder
2. **Fahrzeuge anlegen**: Admin-Bereich → Fahrzeuge
3. **Admin-Passwort ändern**: Admin-Bereich → Passwort ändern

**Standard-Login**: `admin` / `admin` ⚠️ (Bitte sofort ändern!)

## 📖 Verwendung

### Workflow: Anwesenheitsliste

1. **Liste erstellen** (öffentlich, kein Login):
   - Klick auf "Neue Anwesenheitsliste"
   - Titel, Einheit, Beschreibung eingeben
   - "Liste erstellen" → Liste wird geöffnet

2. **Eintragen** (schnell & einfach):
   - Mitgliedsnummer eingeben (z.B. `1234`)
   - Enter drücken ✅
   - ODER Namen eingeben (z.B. `Max`)
   - Enter drücken ✅
   - Falls Mitglied nicht gefunden: Fehlermeldung

3. **Eintrag korrigieren**:
   - 🗑️ Button klicken zum Entfernen
   - Nur möglich, wenn Liste noch offen

4. **Liste abschließen**:
   - "Liste abschließen" Button
   - Keine weiteren Einträge möglich

### Workflow: Einsatzliste

1. **Liste erstellen**:
   - Klick auf "Neue Einsatzliste"
   - Einsatznummer, Stichwort, Alarmzeit
   - "Liste erstellen"

2. **Eintragen**:
   - Mitgliedsnummer/Name eingeben (validiert)
   - Fahrzeug aus Dropdown wählen
   - Funktion wählen
   - Optional: Atemschutz ✓
   - "Eintragen"

3. **Ansicht**:
   - Einträge nach Fahrzeugen sortiert
   - Übersichtliche Gruppierung
   - 🗑️ Löschen bei Bedarf

### Workflow: Geplante Liste

1. **Planen** (Admin):
   - Admin → Geplante Listen
   - "Neue geplante Liste"
   - Typ auswählen (Anwesenheit/Einsatz)
   - Details eingeben
   - **Ereigniszeit**: Wann findet das Event statt?
   - **Minuten vorher**: Wie lange vorher öffnen? (z.B. 30)

2. **Automatisches Öffnen**:
   - Background-Service prüft jede Minute
   - Liste wird automatisch zur berechneten Zeit geöffnet
   - Erscheint automatisch in der Übersicht

3. **Monitoring**:
   - Admin → Service Status
   - Echtzeit-Überwachung des Services
   - Sehen, welche Listen fällig sind

## 🔧 Konfiguration

### Datenbank wechseln

In `appsettings.json`:
```json
{
  "DatabaseSettings": {
    "Provider": "MySQL",  // oder "SQLite"
    "MySQLConnection": "Server=localhost;Database=feuerwehr;User=root;Password=yourpassword;"
  }
}
```

### Logging-Level anpassen

In `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "FeuerwehrListen.Services.ScheduledListBackgroundService": "Information"
    }
  },
  "DetailedErrors": true
}
```

## 🔍 Troubleshooting

### Background-Service läuft nicht

1. Admin → Service Status öffnen
2. Prüfen, ob Listen als "fällig" erkannt werden
3. Logs in der Konsole prüfen
4. "Jetzt öffnen" Button zum manuellen Testen

### Mitglied kann nicht eingetragen werden

1. Admin → Mitglieder
2. Prüfen, ob Mitglied existiert
3. Prüfen, ob Mitglied "Aktiv" ist
4. Mitgliedsnummer oder Namen korrekt eingeben

### Datenbank zurücksetzen

**SQLite**:
```bash
rm FeuerwehrListen/feuerwehr.db
dotnet run --project FeuerwehrListen
```

**MySQL**:
```sql
DROP DATABASE feuerwehr;
CREATE DATABASE feuerwehr;
```

Dann Anwendung neu starten.

## 🛠 Entwicklung

### Code-Richtlinien

- ✅ Repositories mit linq2db
- ✅ Kein JavaScript (nur Blazor)
- ✅ Minimale Kommentare (selbsterklärender Code)
- ✅ API-First Ansatz
- ✅ Alle Models mit linq2db Mapping-Attributen
- ✅ Repository-Pattern für Datenzugriff

### Neue Migration erstellen

Beispiel:
```csharp
[Migration(4)]
public class Migration_004_AddNewFeature : Migration
{
    public override void Up()
    {
        // SQL hier
    }

    public override void Down()
    {
        // Rollback hier
    }
}
```

### Build & Run

```bash
# Build
dotnet build

# Run
cd FeuerwehrListen
dotnet run

# Run mit Hot Reload
dotnet watch run
```

## 🔐 Sicherheit

### Wichtige Hinweise

⚠️ **Standard-Admin**: Das Standard-Passwort `admin`/`admin` sollte sofort geändert werden!

⚠️ **HTTPS**: In Produktion HTTPS verwenden

⚠️ **Passwörter**: Werden mit SHA256 gehasht gespeichert

⚠️ **API Keys**: Sicher aufbewahren und regelmäßig rotieren

### Admin-Bereich schützen

Der Admin-Bereich ist durch die `AdminAuthCheck` Komponente geschützt:
- Prüft `AuthenticationService.IsAdmin`
- Redirect zu `/login` wenn nicht authentifiziert
- Alle Admin-Seiten sind geschützt

## 📊 Datenbank-Schema

### Tabellen

- **AttendanceList**: Anwesenheitslisten
- **AttendanceEntry**: Einträge in Anwesenheitslisten
- **OperationList**: Einsatzlisten  
- **OperationEntry**: Einträge in Einsatzlisten
- **Member**: Feuerwehr-Mitglieder (mit eindeutiger Mitgliedsnummer)
- **Vehicle**: Fahrzeuge (mit Typ und Funkrufname)
- **User**: Admin-Benutzer (mit Passwort-Hash und Rolle)
- **ApiKey**: API-Schlüssel für externe Systeme
- **ScheduledList**: Geplante Listen (mit Auto-Open)

### Enums

- **ListStatus**: Open, Closed
- **OperationFunction**: Maschinist, Gruppenfuehrer, Trupp
- **VehicleType**: LF, TLF, DLK, RW, MTW, KdoW, Sonstige
- **UserRole**: User, Admin
- **ScheduledListType**: Attendance, Operation

## 🎯 Roadmap / Geplante Features

- [ ] PDF-Export von Listen
- [ ] Statistiken und Auswertungen
- [ ] Email-Benachrichtigungen
- [ ] Mehrere Feuerwehr-Einheiten

---

**Entwickelt mit ❤️ und Blazor für die Feuerwehr** 🚒
