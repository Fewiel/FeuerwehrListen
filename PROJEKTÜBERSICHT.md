# 🚒 Feuerwehr Listen - Projektübersicht

## 📊 Projektstatus: Production Ready

**Version**: 1.0  
**Letztes Update**: Oktober 2025  
**Status**: ✅ Vollständig funktionsfähig

---

## 🎯 Projektziel

Moderne Webanwendung zur Verwaltung von Anwesenheits- und Einsatzlisten für Feuerwehren mit:
- Schneller Erfassung von Teilnehmern
- Validierung von Mitgliedern
- Automatischer Öffnung geplanter Listen
- Admin-Bereich für Verwaltung

---

## 🏗️ Architektur

### Technologie-Stack
- **Backend**: C# .NET 8.0, Blazor Server
- **Frontend**: Blazor Components (kein JavaScript!)
- **UI**: Bootstrap 5, Custom Dark Theme
- **Datenbank**: SQLite (Standard) / MySQL (optional)
- **ORM**: linq2db (type-safe)
- **Migrations**: FluentMigrator (automatisch)

### Design Patterns
- **Repository Pattern**: Datenzugriff-Schicht
- **Service Pattern**: Business Logic (Auth, Background)
- **Component-Based**: Blazor Components
- **API-First**: Strukturierte Repositories

---

## 📁 Detaillierte Struktur

```
FeuerwehrListen/
│
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor              # Haupt-Layout (Dark Theme)
│   │   ├── NavMenu.razor                 # Navigation mit Auth-Check
│   │   └── AdminAuthCheck.razor          # Route-Schutz für Admin
│   │
│   └── Pages/
│       ├── Home.razor                     # Übersicht (Auto-Refresh 30s)
│       ├── Login.razor                    # Login-Seite
│       ├── Logout.razor                   # Logout-Handler
│       │
│       ├── CreateAttendance.razor         # Anwesenheitsliste erstellen
│       ├── CreateOperation.razor          # Einsatzliste erstellen
│       ├── AttendanceDetail.razor         # Detail + Eintragen + Löschen
│       ├── OperationDetail.razor          # Detail + Fahrzeug-Sortierung
│       │
│       └── Admin/                         # Admin-Bereich (Login erforderlich)
│           ├── ClosedLists.razor          # Geschlossene Listen
│           ├── Archive.razor              # Archiv
│           ├── Members.razor              # Mitgliederverwaltung
│           ├── Vehicles.razor             # Fahrzeugverwaltung
│           ├── Users.razor                # Benutzerverwaltung + PW-Gen
│           ├── ApiKeys.razor              # API-Schlüssel
│           ├── ScheduledLists.razor       # Geplante Listen (Auto-Refresh 10s)
│           ├── ServiceStatus.razor        # Debug/Monitoring (Auto-Refresh 5s)
│           └── ChangePassword.razor       # Passwort ändern
│
├── Data/
│   └── AppDbConnection.cs                 # linq2db DataConnection
│
├── Migrations/
│   ├── Migration_001_InitialSchema.cs     # Listen, Einträge, User, etc.
│   ├── Migration_002_AddVehicles.cs       # Fahrzeuge
│   └── Migration_003_AddMembers.cs        # Mitglieder
│
├── Models/
│   ├── AttendanceList.cs                  # Anwesenheitsliste (mit Mapping)
│   ├── AttendanceEntry.cs                 # Einträge in Anwesenheitsliste
│   ├── OperationList.cs                   # Einsatzliste (mit Mapping)
│   ├── OperationEntry.cs                  # Einträge in Einsatzliste
│   ├── Member.cs                          # Feuerwehr-Mitglieder
│   ├── Vehicle.cs                         # Fahrzeuge
│   ├── User.cs                            # Admin-Benutzer
│   ├── ApiKey.cs                          # API-Schlüssel
│   ├── ScheduledList.cs                   # Geplante Listen
│   └── Enums.cs                           # Alle Enums
│
├── Repositories/
│   ├── AttendanceListRepository.cs        # CRUD für Anwesenheitslisten
│   ├── AttendanceEntryRepository.cs       # CRUD + Delete für Einträge
│   ├── OperationListRepository.cs         # CRUD für Einsatzlisten
│   ├── OperationEntryRepository.cs        # CRUD + Delete für Einträge
│   ├── MemberRepository.cs                # CRUD + FindByNameOrNumber
│   ├── VehicleRepository.cs               # CRUD + GetActive
│   ├── UserRepository.cs                  # CRUD + GetByUsername
│   ├── ApiKeyRepository.cs                # CRUD für API-Keys
│   └── ScheduledListRepository.cs         # CRUD + GetDue
│
├── Services/
│   ├── AuthenticationService.cs           # Login, Logout, Passwort ändern
│   └── ScheduledListBackgroundService.cs  # Auto-Öffnen (prüft jede Minute)
│
└── wwwroot/
    ├── app.css                            # Custom Dark Theme
    ├── favicon.svg                        # Feuerwehr-Icon
    └── bootstrap/                         # Bootstrap 5
```

---

## 🔄 Datenfluss

### Eintragen in Liste (Beispiel)

```
Benutzer gibt Mitgliedsnummer ein
         ↓
AttendanceDetail.razor (Component)
         ↓
MemberRepository.FindByNameOrNumberAsync()
         ↓
linq2db Query → Datenbank
         ↓
Member gefunden? 
  ✅ Ja → AttendanceEntryRepository.CreateAsync()
  ❌ Nein → Fehlermeldung anzeigen
         ↓
Liste wird neu geladen
         ↓
UI aktualisiert sich (StateHasChanged)
```

### Geplante Listen (Background-Service)

```
ScheduledListBackgroundService läuft alle 60s
         ↓
ScheduledListRepository.GetDueAsync()
  → Findet alle Listen wo: OpenTime <= NOW
         ↓
Für jede fällige Liste:
  1. AttendanceList/OperationList erstellen
  2. ScheduledList als "Processed" markieren
  3. Log-Eintrag erstellen
         ↓
Home.razor Auto-Refresh (alle 30s)
  → Zeigt neue Liste automatisch an
```

---

## 🗃️ Datenbank-Schema

### Kern-Tabellen

**AttendanceList** (Anwesenheitslisten)
- Id, Title, Unit, Description
- CreatedAt, Status, ClosedAt, IsArchived

**AttendanceEntry** (Einträge)
- Id, AttendanceListId, NameOrId
- EnteredAt

**OperationList** (Einsatzlisten)
- Id, OperationNumber, Keyword, AlertTime
- CreatedAt, Status, ClosedAt, IsArchived

**OperationEntry** (Einträge)
- Id, OperationListId, NameOrId
- Vehicle, Function, WithBreathingApparatus
- EnteredAt

### Stammdaten

**Member** (Mitglieder)
- Id, MemberNumber (unique), FirstName, LastName
- IsActive, CreatedAt

**Vehicle** (Fahrzeuge)
- Id, Name, CallSign, Type
- IsActive, CreatedAt

### System

**User** (Admin-Benutzer)
- Id, Username, PasswordHash, Role
- FirstName, LastName, Email, CreatedAt

**ApiKey** (API-Schlüssel)
- Id, Key, Description
- IsActive, CreatedAt

**ScheduledList** (Geplante Listen)
- Id, Type, Title, Unit, Description
- OperationNumber, Keyword
- ScheduledEventTime, MinutesBeforeEvent
- IsProcessed, CreatedAt

### Enums

```csharp
enum ListStatus { Open = 0, Closed = 1 }
enum OperationFunction { Maschinist = 0, Gruppenfuehrer = 1, Trupp = 2 }
enum VehicleType { LF, TLF, DLK, RW, MTW, KdoW, Sonstige }
enum UserRole { User = 1, Admin = 2 }
enum ScheduledListType { Attendance = 0, Operation = 1 }
```

---

## 🔐 Authentifizierung & Autorisierung

### AuthenticationService (Singleton)

**Funktionen:**
- `LoginAsync(username, password)` → Validiert Credentials
- `Logout()` → Löscht Session
- `ChangePasswordAsync(oldPassword, newPassword)` → Passwort ändern
- `HashPassword(password)` → SHA256 Hashing
- `IsAdmin { get; }` → Property für Auth-Check

**Events:**
- `OnAuthStateChanged` → Für UI-Updates

### AdminAuthCheck Component

```razor
@if (AuthService.IsAdmin)
{
    @ChildContent
}
else
{
    Navigation.NavigateTo("/login", false);
}
```

Schützt alle Admin-Routen automatisch.

---

## ⚙️ Wichtige Features

### 1. Auto-Refresh System

| Seite | Intervall | Implementierung |
|-------|-----------|-----------------|
| Übersicht | 30s | `System.Threading.Timer` |
| Geplante Listen | 10s | `System.Threading.Timer` |
| Service Status | 5s | `System.Threading.Timer` |

**Implementierung:**
```csharp
_refreshTimer = new System.Threading.Timer(async _ => 
{
    await InvokeAsync(async () => 
    {
        await LoadData();
        StateHasChanged();
    });
}, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
```

### 2. Mitglieder-Validierung

**FindByNameOrNumberAsync:**
```csharp
var member = await _db.Members
    .Where(x => x.IsActive && (
        x.MemberNumber == term ||
        (x.FirstName + " " + x.LastName).Contains(term) ||
        (x.LastName + " " + x.FirstName).Contains(term)
    ))
    .FirstOrDefaultAsync();
```

Findet Mitglieder nach:
- Exakter Mitgliedsnummer
- Teilname im Namen (Vorname + Nachname)
- Teilname im Namen (Nachname + Vorname)
- Nur aktive Mitglieder

### 3. Fahrzeug-Sortierung

Bei Einsatzlisten werden Einträge nach Fahrzeugen gruppiert:

```csharp
@foreach (var vehicleGroup in _entries
    .GroupBy(e => e.Vehicle)
    .OrderBy(g => g.Key))
{
    <h6>🚒 @vehicleGroup.Key (@vehicleGroup.Count())</h6>
    // Tabelle mit Einträgen für dieses Fahrzeug
}
```

### 4. Passwort-Generierung

**Beim Anlegen neuer Benutzer:**
- 12 Zeichen: A-Z, a-z, 2-9, !@#$%&*
- Keine verwirrenden Zeichen (0/O, 1/l/I)
- Einmalige Anzeige mit Kopier-Button
- SHA256 Hash in Datenbank

### 5. Background-Service

**ScheduledListBackgroundService:**
- Startet automatisch mit Anwendung
- Prüft alle 60 Sekunden auf fällige Listen
- Öffnet Listen zur berechneten Zeit
- Logging für Debugging

**Berechnung:**
```
Öffnungszeit = EventTime - MinutesBeforeEvent
Fällig wenn: Öffnungszeit <= DateTime.Now
```

---

## 🔧 Konfiguration

### appsettings.json

```json
{
  "DatabaseSettings": {
    "Provider": "SQLite",
    "SQLiteConnection": "Data Source=feuerwehr.db",
    "MySQLConnection": "Server=localhost;Database=feuerwehr;..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "FeuerwehrListen.Services.ScheduledListBackgroundService": "Information"
    }
  }
}
```

### Program.cs Konfiguration

**Services:**
- Scoped: Repositories (pro Request)
- Singleton: AuthenticationService (App-Lifetime)
- Hosted: ScheduledListBackgroundService (Background)

**Middleware:**
- HttpsRedirection
- StaticFiles
- Antiforgery
- RazorComponents (InteractiveServer)

---

## 📊 Performance-Überlegungen

### Optimierungen

1. **linq2db**: Schnelle, type-safe Queries
2. **Auto-Refresh**: Nur sichtbare Seiten laden neu
3. **Scoped Repositories**: Pro Request → Memory efficient
4. **Blazor Server**: Weniger Client-Last
5. **Indexes**: Unique auf MemberNumber

### Limitations

- **Blazor Server**: Benötigt aktive Verbindung
- **Auto-Refresh**: Network Traffic bei vielen Nutzern
- **Background-Service**: 60s Intervall (kann angepasst werden)

---

## 🧪 Testing-Strategie

### Manuelle Tests

**Checkliste:**
- [ ] Login/Logout funktioniert
- [ ] Mitglieder können angelegt werden
- [ ] Fahrzeuge können angelegt werden
- [ ] Anwesenheitsliste erstellen und eintragen
- [ ] Einsatzliste erstellen und eintragen
- [ ] Mitglieder-Validierung funktioniert
- [ ] Einträge können gelöscht werden
- [ ] Listen können abgeschlossen werden
- [ ] Geplante Listen werden automatisch geöffnet
- [ ] Auto-Refresh funktioniert auf allen Seiten
- [ ] Passwort ändern funktioniert
- [ ] Passwort-Generator funktioniert

### Service-Monitoring

**Service Status Seite:**
- Zeigt fällige Listen
- Zeigt Background-Service Status
- Echtzeit-Zeitberechnung
- "Jetzt öffnen" Test-Button

---

## 🚀 Deployment

### Voraussetzungen

- .NET 8.0 Runtime
- Webserver (IIS, Nginx, Kestrel)
- Optional: MySQL Server

### Schritte

1. **Build:**
```bash
dotnet publish -c Release -o ./publish
```

2. **Datenbank-Config:**
```json
{
  "DatabaseSettings": {
    "Provider": "SQLite",
    "SQLiteConnection": "Data Source=/var/feuerwehr/feuerwehr.db"
  }
}
```

3. **Erster Start:**
- Migrationen laufen automatisch
- Standard-Admin: `admin` / `admin`
- ⚠️ Passwort sofort ändern!

4. **Reverse Proxy (nginx):**
```nginx
location / {
    proxy_pass http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
}
```

---

## 📊 PDF-Export & Statistiken

### PDF-Export Features
- **Anwesenheitslisten**: Vollständige Listen mit allen Einträgen
- **Einsatzlisten**: Nach Fahrzeugen gruppiert, mit Funktionen und Atemschutz
- **Statistik-Berichte**: Umfassende Auswertungen mit Diagrammen
- **Kartenintegration**: OpenStreetMap-Ausschnitte in Einsatz-PDFs
- **Custom Fonts**: CreatoDisplay-Schriftarten für professionelles Aussehen

### Statistik-Features
- **Übersicht**: Gesamt-KPIs (Listen, Teilnehmer, Durchschnitte)
- **Top Teilnehmer**: Top 10 aktivste Mitglieder
- **Fahrzeug-Nutzung**: Einsatzstatistiken pro Fahrzeug
- **Funktionen-Verteilung**: Analyse der Funktionsbesetzung
- **Atemschutz**: Statistiken zum Atemschutz-Einsatz
- **Trend-Daten**: Monatliche Entwicklungen

### Live-Suche
- **Debounced Search**: Intelligente Mitgliedersuche während der Eingabe
- **Fuzzy-Matching**: Findet Mitglieder auch bei Teilnamen
- **Dropdown-Vorschläge**: Auto-Complete für schnelles Eintragen

## 🗺️ Geocoding & Kartenintegration

### OpenStreetMap-Integration
- **Adresseingabe**: Einsatzadressen erfassen und speichern
- **Geocoding**: Automatische Umwandlung in Koordinaten (Nominatim)
- **Live-Karte**: Interaktive OSM-Karte im Einsatzdetail
- **PDF-Karte**: Statischer Kartenausschnitt mit Marker im PDF
- **Multi-Provider**: Fallback-Mechanismen für Kartenrendering

## 🔧 Dynamische Funktionen

### Operation Functions Management
- **Admin-Konfiguration**: Funktionen zentral verwalten
- **Standardfunktionen**: Atemschutzgeräteträger, Gruppenführer, Maschinist
- **Erweiterbar**: Neue Funktionen jederzeit hinzufügen
- **Mehrfachauswahl**: Ein Mitglied kann mehrere Funktionen haben
- **Join-Table**: Flexible Many-to-Many-Beziehung

## 📈 Erweiterungsmöglichkeiten

### Kurzfristig
- ✅ PDF-Export (implementiert)
- ✅ Statistiken (implementiert)
- ✅ API für externe Systeme (implementiert)
- Excel-Export
- Email-Benachrichtigungen

### Mittelfristig
- Push-Notifications
- QR-Code-Scanner für Mitglieder
- Mobile App (Blazor Hybrid)
- Mehrere Feuerwehr-Einheiten

### Langfristig
- Schnittstelle zu Alarmierungssystemen
- Dienstplan-Integration
- Ausrüstungsverwaltung

---

## 🔍 Troubleshooting

### Häufige Probleme

**Problem:** Background-Service läuft nicht
- **Lösung:** Service Status Seite prüfen, Logs checken

**Problem:** Mitglied kann nicht eingetragen werden
- **Lösung:** Mitglied existiert? Ist aktiv? Schreibweise korrekt?

**Problem:** Auto-Refresh funktioniert nicht
- **Lösung:** SignalR-Verbindung ok? Browser-Console prüfen

**Problem:** Login funktioniert nicht
- **Lösung:** Passwort korrekt? Caps Lock? Browser-Cache leeren

### Debug-Tools

1. **Service Status Seite**: Real-time Monitoring
2. **Browser DevTools**: Console für Blazor-Fehler
3. **Application Logs**: appsettings.json LogLevel anpassen
4. **Datenbank**: SQLite Browser / MySQL Workbench

---

## 👥 Team & Kontakt

**Entwicklung:** Blazor + C# .NET 8.0  
**Zweck:** Feuerwehr-interne Nutzung  
**Support:** Siehe Repository Issues

---

## 📚 Weitere Dokumentation

- **README.md**: Installations- und Benutzerhandbuch (siehe separate Datei)
- **API-DOCUMENTATION.md**: Vollständige REST API-Dokumentation (siehe separate Datei)
- **Code**: Minimale Kommentare (selbsterklärender Code, keine Code-Redundanz)

---

**Letztes Update:** Oktober 2025  
**Status:** ✅ Production Ready  
**Features:** PDF-Export ✅, Statistiken ✅, REST API ✅, Geocoding ✅
