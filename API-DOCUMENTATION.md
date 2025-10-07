# 🚒 Feuerwehr Listen - REST API Dokumentation

## 📖 Übersicht

Die Feuerwehr Listen API ermöglicht externen Systemen die Anbindung an die Anwendung über HTTP-Requests. 

**Base URL:** `https://your-domain.com/api`  
**Authentifizierung:** API-Key (Header: `X-API-Key`)  
**Datenformat:** JSON  
**Version:** v1.0

---

## 🔐 Authentifizierung

Alle API-Endpunkte erfordern einen **API-Key** im Request-Header.

### API-Key erstellen

1. Login als Admin in der Web-App
2. Navigiere zu **Admin → API-Schlüssel**
3. Erstelle einen neuen API-Key
4. Key wird einmalig angezeigt → notieren!

### Request Header

```http
X-API-Key: your-api-key-here
Content-Type: application/json
```

### Fehler bei fehlender/ungültiger Authentifizierung

```json
HTTP 401 Unauthorized
{
  "error": "API Key missing"
}
```

```json
HTTP 401 Unauthorized
{
  "error": "Invalid or inactive API Key"
}
```

---

## 📋 Response Format

Alle erfolgreichen Responses folgen diesem Schema:

```json
{
  "success": true,
  "message": "Optional success message",
  "data": { ... }
}
```

### Fehler-Responses

```json
{
  "error": "Short error description",
  "details": "Optional detailed error message"
}
```

**HTTP Status Codes:**
- `200 OK` - Erfolgreiche Anfrage
- `201 Created` - Ressource erstellt
- `400 Bad Request` - Ungültige Anfrage
- `401 Unauthorized` - API-Key fehlt/ungültig
- `404 Not Found` - Ressource nicht gefunden
- `500 Internal Server Error` - Server-Fehler

---

## 🔗 API-Endpunkte

### 1. Anwesenheitslisten

#### 1.1 Offene Listen abrufen

```http
GET /api/attendance/lists
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "title": "Übung Atemschutz",
      "createdAt": "2025-10-07T19:00:00"
    },
    {
      "id": 2,
      "title": "Fahrzeugpflege",
      "createdAt": "2025-10-07T14:30:00"
    }
  ]
}
```

**cURL Beispiel:**
```bash
curl -X GET https://your-domain.com/api/attendance/lists \
  -H "X-API-Key: your-api-key-here"
```

---

#### 1.2 Liste Details abrufen

```http
GET /api/attendance/lists/{id}
```

**Parameter:**
- `id` (path) - Listen-ID

**Response:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "title": "Übung Atemschutz",
    "unit": "Löschzug 1",
    "description": "Monatliche Atemschutz-Übung",
    "createdAt": "2025-10-07T19:00:00",
    "status": 0,
    "closedAt": null,
    "isArchived": false
  }
}
```

---

#### 1.3 Neue Liste erstellen

```http
POST /api/attendance/lists
```

**Request Body:**
```json
{
  "title": "Übung Atemschutz",
  "unit": "Löschzug 1",
  "description": "Monatliche Atemschutz-Übung"
}
```

**Response:**
```json
{
  "success": true,
  "message": "List created successfully",
  "data": {
    "id": 3,
    "title": "Übung Atemschutz",
    "createdAt": "2025-10-07T19:00:00"
  }
}
```

**cURL Beispiel:**
```bash
curl -X POST https://your-domain.com/api/attendance/lists \
  -H "X-API-Key: your-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Übung Atemschutz",
    "unit": "Löschzug 1",
    "description": "Monatliche Atemschutz-Übung"
  }'
```

---

#### 1.4 Einträge einer Liste abrufen

```http
GET /api/attendance/lists/{listId}/entries
```

**Parameter:**
- `listId` (path) - Listen-ID

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "listId": 1,
      "nameOrId": "Max Mustermann (1234)",
      "enteredAt": "2025-10-07T19:05:00"
    },
    {
      "id": 2,
      "listId": 1,
      "nameOrId": "Anna Schmidt (5678)",
      "enteredAt": "2025-10-07T19:06:30"
    }
  ]
}
```

---

#### 1.5 Eintrag hinzufügen

```http
POST /api/attendance/lists/{listId}/entries
```

**Parameter:**
- `listId` (path) - Listen-ID

**Request Body:**
```json
{
  "memberNumberOrName": "1234"
}
```

**Alternative:** Name statt Nummer
```json
{
  "memberNumberOrName": "Max Mustermann"
}
```

**Response (Erfolg):**
```json
{
  "success": true,
  "message": "Entry added successfully",
  "data": {
    "id": 3,
    "listId": 1,
    "nameOrId": "Max Mustermann (1234)",
    "enteredAt": "2025-10-07T19:10:00"
  }
}
```

**Fehler (Mitglied nicht gefunden):**
```json
HTTP 400 Bad Request
{
  "error": "Member not found",
  "details": "No active member found for: 9999"
}
```

**Fehler (Liste geschlossen):**
```json
HTTP 400 Bad Request
{
  "error": "List is closed"
}
```

**cURL Beispiel:**
```bash
curl -X POST https://your-domain.com/api/attendance/lists/1/entries \
  -H "X-API-Key: your-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{"memberNumberOrName": "1234"}'
```

---

#### 1.6 Liste schließen

```http
POST /api/attendance/lists/{listId}/close
```

**Parameter:**
- `listId` (path) - Listen-ID

**Response:**
```json
{
  "success": true,
  "message": "List closed successfully",
  "data": "2025-10-07 20:00:00"
}
```

---

#### 1.7 Eintrag löschen

```http
DELETE /api/attendance/lists/{listId}/entries/{entryId}
```

**Parameter:**
- `listId` (path) - Listen-ID
- `entryId` (path) - Eintrags-ID

**Response:**
```json
{
  "success": true,
  "message": "Entry deleted successfully"
}
```

**Fehler (Liste geschlossen):**
```json
HTTP 400 Bad Request
{
  "error": "Cannot delete entry from closed list"
}
```

---

### 2. Einsatzlisten

#### 2.1 Offene Listen abrufen

```http
GET /api/operation/lists
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "title": "2025-100",
      "createdAt": "2025-10-07T14:23:00"
    }
  ]
}
```

---

#### 2.2 Liste Details abrufen

```http
GET /api/operation/lists/{id}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "operationNumber": "2025-100",
    "keyword": "Brand 3 - Wohngebäude",
    "alertTime": "2025-10-07T14:20:00",
    "createdAt": "2025-10-07T14:23:00",
    "status": 0,
    "closedAt": null,
    "isArchived": false
  }
}
```

---

#### 2.3 Neue Liste erstellen

```http
POST /api/operation/lists
```

**Request Body:**
```json
{
  "operationNumber": "2025-100",
  "keyword": "Brand 3 - Wohngebäude",
  "alertTime": "2025-10-07T14:20:00"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Operation list created successfully",
  "data": {
    "id": 2,
    "title": "2025-100",
    "createdAt": "2025-10-07T14:23:00"
  }
}
```

**cURL Beispiel:**
```bash
curl -X POST https://your-domain.com/api/operation/lists \
  -H "X-API-Key: your-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{
    "operationNumber": "2025-100",
    "keyword": "Brand 3 - Wohngebäude",
    "alertTime": "2025-10-07T14:20:00"
  }'
```

---

#### 2.4 Einträge einer Liste abrufen

```http
GET /api/operation/lists/{listId}/entries
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "listId": 1,
      "nameOrId": "Max Mustermann (1234)",
      "vehicle": "LF 20",
      "function": "Maschinist",
      "withBreathingApparatus": false,
      "enteredAt": "2025-10-07T14:25:00"
    },
    {
      "id": 2,
      "listId": 1,
      "nameOrId": "Anna Schmidt (5678)",
      "vehicle": "LF 20",
      "function": "Trupp",
      "withBreathingApparatus": true,
      "enteredAt": "2025-10-07T14:26:00"
    }
  ]
}
```

---

#### 2.5 Eintrag hinzufügen

```http
POST /api/operation/lists/{listId}/entries
```

**Request Body:**
```json
{
  "memberNumberOrName": "1234",
  "vehicle": "LF 20",
  "function": 0,
  "withBreathingApparatus": false
}
```

**Funktion-Werte:**
- `0` = Maschinist
- `1` = Gruppenführer
- `2` = Trupp

**Response:**
```json
{
  "success": true,
  "message": "Entry added successfully",
  "data": {
    "id": 3,
    "listId": 1,
    "nameOrId": "Max Mustermann (1234)",
    "vehicle": "LF 20",
    "function": "Maschinist",
    "withBreathingApparatus": false,
    "enteredAt": "2025-10-07T14:30:00"
  }
}
```

**Fehler (Fahrzeug nicht gefunden):**
```json
HTTP 400 Bad Request
{
  "error": "Vehicle not found",
  "details": "No active vehicle found: XYZ"
}
```

**cURL Beispiel:**
```bash
curl -X POST https://your-domain.com/api/operation/lists/1/entries \
  -H "X-API-Key: your-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{
    "memberNumberOrName": "1234",
    "vehicle": "LF 20",
    "function": 0,
    "withBreathingApparatus": false
  }'
```

---

#### 2.6 Liste schließen

```http
POST /api/operation/lists/{listId}/close
```

**Response:**
```json
{
  "success": true,
  "message": "Operation list closed successfully",
  "data": "2025-10-07 16:00:00"
}
```

---

#### 2.7 Eintrag löschen

```http
DELETE /api/operation/lists/{listId}/entries/{entryId}
```

**Response:**
```json
{
  "success": true,
  "message": "Entry deleted successfully"
}
```

---

#### 2.8 Verfügbare Fahrzeuge abrufen

```http
GET /api/operation/vehicles
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "name": "LF 20",
      "callSign": "Florian 1-44-1",
      "type": 0,
      "isActive": true,
      "createdAt": "2025-10-01T10:00:00"
    },
    {
      "id": 2,
      "name": "DLK 23",
      "callSign": "Florian 1-44-2",
      "type": 2,
      "isActive": true,
      "createdAt": "2025-10-01T10:00:00"
    }
  ]
}
```

**Fahrzeug-Typen:**
- `0` = LF (Löschfahrzeug)
- `1` = TLF (Tanklöschfahrzeug)
- `2` = DLK (Drehleiter)
- `3` = RW (Rüstwagen)
- `4` = MTW (Mannschaftstransportwagen)
- `5` = KdoW (Kommandowagen)
- `6` = Sonstige

---

### 3. Mitglieder

#### 3.1 Mitglied suchen

```http
GET /api/members/search?q={searchTerm}
```

**Query Parameter:**
- `q` - Mitgliedsnummer ODER Name (teilweise)

**Beispiele:**
```
GET /api/members/search?q=1234
GET /api/members/search?q=Max
GET /api/members/search?q=Mustermann
```

**Response (gefunden):**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "memberNumber": "1234",
    "firstName": "Max",
    "lastName": "Mustermann",
    "fullName": "Max Mustermann"
  }
}
```

**Fehler (nicht gefunden):**
```json
HTTP 404 Not Found
{
  "error": "Member not found",
  "details": "No active member found for: 9999"
}
```

**cURL Beispiel:**
```bash
curl -X GET "https://your-domain.com/api/members/search?q=1234" \
  -H "X-API-Key: your-api-key-here"
```

---

#### 3.2 Alle aktiven Mitglieder abrufen

```http
GET /api/members
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "memberNumber": "1234",
      "firstName": "Max",
      "lastName": "Mustermann",
      "fullName": "Max Mustermann"
    },
    {
      "id": 2,
      "memberNumber": "5678",
      "firstName": "Anna",
      "lastName": "Schmidt",
      "fullName": "Anna Schmidt"
    }
  ]
}
```

---

### 4. Geplante Listen

#### 4.1 Geplante Liste erstellen

```http
POST /api/scheduled/lists
```

**Request Body (Anwesenheitsliste):**
```json
{
  "type": 0,
  "title": "Monatsübung November",
  "unit": "Löschzug 1",
  "description": "Atemschutz-Übung",
  "scheduledEventTime": "2025-11-01T19:00:00",
  "minutesBeforeEvent": 30
}
```

**Request Body (Einsatzliste):**
```json
{
  "type": 1,
  "operationNumber": "Geplant-001",
  "keyword": "Sicherheitswache Veranstaltung",
  "scheduledEventTime": "2025-11-05T18:00:00",
  "minutesBeforeEvent": 60
}
```

**Listen-Typen:**
- `0` = Attendance (Anwesenheit)
- `1` = Operation (Einsatz)

**Response:**
```json
{
  "success": true,
  "message": "Scheduled list created successfully",
  "data": 3
}
```

**Hinweis:** Die Liste wird automatisch **X Minuten vor** der Event-Zeit geöffnet.

**cURL Beispiel:**
```bash
curl -X POST https://your-domain.com/api/scheduled/lists \
  -H "X-API-Key: your-api-key-here" \
  -H "Content-Type: application/json" \
  -d '{
    "type": 0,
    "title": "Monatsübung November",
    "unit": "Löschzug 1",
    "description": "Atemschutz-Übung",
    "scheduledEventTime": "2025-11-01T19:00:00",
    "minutesBeforeEvent": 30
  }'
```

---

#### 4.2 Ausstehende geplante Listen abrufen

```http
GET /api/scheduled/lists
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "type": 0,
      "title": "Monatsübung November",
      "unit": "Löschzug 1",
      "description": "Atemschutz-Übung",
      "scheduledEventTime": "2025-11-01T19:00:00",
      "minutesBeforeEvent": 30,
      "isProcessed": false,
      "createdAt": "2025-10-07T10:00:00"
    }
  ]
}
```

---

### 5. Statistiken

#### 5.1 Übersicht abrufen

```http
GET /api/statistics/overview
```

**Response:**
```json
{
  "success": true,
  "data": {
    "totalLists": 25,
    "openLists": 3,
    "closedLists": 20,
    "archivedLists": 2,
    "averageParticipants": 8.5,
    "totalParticipants": 212,
    "lastListCreated": "2025-10-07T19:00:00"
  }
}
```

---

#### 5.2 Top Teilnehmer abrufen

```http
GET /api/statistics/top-participants?limit=10
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "memberId": 1,
      "memberName": "Max Mustermann",
      "memberNumber": "1234",
      "participationCount": 15,
      "percentage": 7.1
    }
  ]
}
```

---

#### 5.3 Mitglieder-Statistiken abrufen

```http
GET /api/statistics/member-statistics
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "memberId": 1,
      "memberName": "Max Mustermann",
      "memberNumber": "1234",
      "totalAttendance": 10,
      "totalOperations": 5,
      "attendancePercentage": 75.0,
      "lastParticipation": "2025-10-07T19:00:00",
      "monthlyData": [
        {
          "year": 2025,
          "month": 10,
          "attendanceCount": 2,
          "operationCount": 1,
          "percentage": 100.0
        }
      ]
    }
  ]
}
```

---

#### 5.4 Trend-Daten abrufen

```http
GET /api/statistics/trend-data?months=12
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "date": "2025-10-07T00:00:00",
      "attendanceCount": 1,
      "operationCount": 0,
      "totalParticipants": 8
    }
  ]
}
```

---

#### 5.5 Fahrzeug-Statistiken abrufen

```http
GET /api/statistics/vehicle-statistics
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "vehicleName": "LF 20",
      "usageCount": 15,
      "usagePercentage": 45.5,
      "totalCrewMembers": 45
    }
  ]
}
```

---

#### 5.6 Funktionen-Statistiken abrufen

```http
GET /api/statistics/function-statistics
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "functionName": "Trupp",
      "count": 25,
      "percentage": 50.0
    }
  ]
}
```

---

#### 5.7 Atemschutz-Statistiken abrufen

```http
GET /api/statistics/breathing-apparatus-statistics
```

**Response:**
```json
{
  "success": true,
  "data": {
    "withApparatus": 15,
    "withoutApparatus": 35,
    "withApparatusPercentage": 30.0
  }
}
```

---

### 6. PDF Export

#### 6.1 Anwesenheitsliste als PDF exportieren

```http
GET /api/export/attendance/{listId}/pdf
```

**Parameter:**
- `listId` (path) - Listen-ID

**Response:** PDF-Datei (binary)

**cURL Beispiel:**
```bash
curl -X GET https://your-domain.com/api/export/attendance/1/pdf \
  -H "X-API-Key: your-api-key-here" \
  -o "anwesenheitsliste_1.pdf"
```

---

#### 6.2 Einsatzliste als PDF exportieren

```http
GET /api/export/operation/{listId}/pdf
```

**Parameter:**
- `listId` (path) - Listen-ID

**Response:** PDF-Datei (binary)

**cURL Beispiel:**
```bash
curl -X GET https://your-domain.com/api/export/operation/1/pdf \
  -H "X-API-Key: your-api-key-here" \
  -o "einsatzliste_1.pdf"
```

---

#### 6.3 Statistik-Bericht als PDF exportieren

```http
GET /api/export/statistics/pdf
```

**Response:** PDF-Datei (binary)

**cURL Beispiel:**
```bash
curl -X GET https://your-domain.com/api/export/statistics/pdf \
  -H "X-API-Key: your-api-key-here" \
  -o "statistikbericht.pdf"
```

---

## 🔧 Swagger UI (Development)

Im Development-Modus ist eine **interaktive API-Dokumentation** verfügbar:

**URL:** `https://your-domain.com/swagger`

### Features:
- Alle Endpunkte übersichtlich
- "Try it out" - Direktes Testen
- API-Key Authentifizierung
- Request/Response Beispiele

---

## 💡 Integration-Beispiele

### Beispiel 1: Alarmierungssystem erstellt automatisch Einsatzliste

```python
import requests
import json
from datetime import datetime

API_BASE = "https://feuerwehr.example.com/api"
API_KEY = "your-api-key-here"
HEADERS = {
    "X-API-Key": API_KEY,
    "Content-Type": "application/json"
}

# Alarmierung wurde empfangen
alarm_data = {
    "operationNumber": "2025-123",
    "keyword": "Brand 3 - Wohngebäude",
    "alertTime": datetime.now().isoformat()
}

# Einsatzliste erstellen
response = requests.post(
    f"{API_BASE}/operation/lists",
    headers=HEADERS,
    json=alarm_data
)

if response.status_code == 201:
    list_id = response.json()["data"]["id"]
    print(f"Einsatzliste erstellt: ID {list_id}")
else:
    print(f"Fehler: {response.json()}")
```

---

### Beispiel 2: Zugangssystem trägt Mitglied automatisch ein

```javascript
const axios = require('axios');

const API_BASE = 'https://feuerwehr.example.com/api';
const API_KEY = 'your-api-key-here';

async function checkInMember(listId, memberNumber) {
  try {
    const response = await axios.post(
      `${API_BASE}/attendance/lists/${listId}/entries`,
      { memberNumberOrName: memberNumber },
      { headers: { 'X-API-Key': API_KEY } }
    );
    
    console.log(`✅ ${response.data.data.nameOrId} eingetragen`);
    return response.data;
  } catch (error) {
    console.error(`❌ Fehler: ${error.response.data.error}`);
  }
}

// Mitglied 1234 scannt seine Karte am Eingang
checkInMember(1, '1234');
```

---

### Beispiel 3: Externes System prüft offene Listen

```bash
#!/bin/bash

API_BASE="https://feuerwehr.example.com/api"
API_KEY="your-api-key-here"

# Alle offenen Anwesenheitslisten abrufen
curl -X GET "$API_BASE/attendance/lists" \
  -H "X-API-Key: $API_KEY" \
  | jq '.data[] | {id, title, createdAt}'

# Alle offenen Einsatzlisten abrufen
curl -X GET "$API_BASE/operation/lists" \
  -H "X-API-Key: $API_KEY" \
  | jq '.data[] | {id, title, createdAt}'
```

---

### Beispiel 4: Automatische Listen-Erstellung für regelmäßige Übungen

```python
import requests
from datetime import datetime, timedelta

API_BASE = "https://feuerwehr.example.com/api"
API_KEY = "your-api-key-here"
HEADERS = {
    "X-API-Key": API_KEY,
    "Content-Type": "application/json"
}

# Jeden ersten Freitag im Monat: Atemschutz-Übung
next_training = datetime(2025, 11, 7, 19, 0)  # 07.11.2025 19:00

scheduled_list = {
    "type": 0,  # Attendance
    "title": "Atemschutz-Übung November",
    "unit": "Löschzug 1",
    "description": "Monatliche Atemschutz-Übung",
    "scheduledEventTime": next_training.isoformat(),
    "minutesBeforeEvent": 30  # Liste öffnet 30 Min vorher
}

response = requests.post(
    f"{API_BASE}/scheduled/lists",
    headers=HEADERS,
    json=scheduled_list
)

print(f"Geplante Liste erstellt: {response.json()}")
```

---

## 🚨 Fehlerbehandlung

### Best Practices

1. **Immer HTTP Status Codes prüfen**
```python
if response.status_code == 200:
    # Erfolg
elif response.status_code == 401:
    # API-Key ungültig
elif response.status_code == 404:
    # Ressource nicht gefunden
```

2. **Fehler-Details auswerten**
```python
error_data = response.json()
print(f"Error: {error_data['error']}")
if 'details' in error_data:
    print(f"Details: {error_data['details']}")
```

3. **Retry-Logik für temporäre Fehler**
```python
import time

def api_call_with_retry(url, max_retries=3):
    for attempt in range(max_retries):
        try:
            response = requests.get(url, headers=HEADERS)
            if response.status_code == 200:
                return response.json()
        except requests.RequestException as e:
            if attempt == max_retries - 1:
                raise
            time.sleep(2 ** attempt)  # Exponential backoff
```

---

## 📊 Rate Limiting

**Aktuell:** Keine Rate-Limits implementiert

**Geplant:**
- 100 Requests pro Minute pro API-Key
- 429 Too Many Requests Response

---

## 🔒 Sicherheit

### Best Practices

1. **API-Keys sicher speichern**
   - Niemals in Code einchecken
   - Umgebungsvariablen verwenden
   - Secrets Management nutzen

2. **HTTPS verwenden**
   - Alle API-Calls über HTTPS
   - Keine unverschlüsselte Übertragung

3. **API-Keys rotieren**
   - Regelmäßig neue Keys erstellen
   - Alte Keys deaktivieren

4. **Logging/Monitoring**
   - API-Zugriffe loggen
   - Verdächtige Aktivitäten überwachen

---

## 📝 Änderungsprotokoll

### Version 1.0 (Oktober 2025)
- ✅ Initiale API-Implementierung
- ✅ Anwesenheitslisten-Endpunkte
- ✅ Einsatzlisten-Endpunkte
- ✅ Mitglieder-Suche
- ✅ Geplante Listen
- ✅ API-Key Authentifizierung
- ✅ Swagger UI

### Geplant für v1.1
- [ ] Archiv-Endpunkte (GET)
- [ ] Statistiken-Endpunkte
- [ ] Webhooks für Events
- [ ] Rate Limiting
- [ ] API-Nutzungsstatistiken

---

## 🆘 Support

### Häufige Fehler

**Problem:** `401 Unauthorized`  
**Lösung:** API-Key prüfen, Header korrekt setzen

**Problem:** `400 Bad Request - Member not found`  
**Lösung:** Mitglied existiert nicht oder ist inaktiv

**Problem:** `400 Bad Request - List is closed`  
**Lösung:** Liste wurde bereits geschlossen, keine Einträge mehr möglich

**Problem:** Swagger UI nicht erreichbar  
**Lösung:** Nur im Development-Modus verfügbar

---

## 📚 Weitere Ressourcen

- **README.md** - Benutzerhandbuch & Installation
- **PROJEKTÜBERSICHT.md** - Technische Architektur
- **Swagger UI** - Interaktive API-Doku (Dev-Modus)

---

**API Version:** 1.0  
**Letzte Aktualisierung:** Oktober 2025  
**Support:** Siehe Repository
