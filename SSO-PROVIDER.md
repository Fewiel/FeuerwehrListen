# 🔑 Feuerwehr Listen - SSO-Provider (OAuth2)

## 📖 Übersicht

Feuerwehr Listen kann als **Identity-Provider (IdP)** für andere interne Systeme dienen
(z. B. „Alarmmonitor"). Ein externes System leitet seine Nutzer zum Login zu Feuerwehr Listen
weiter, gibt an welche **Berechtigungskeys** es benötigt, und erhält nach erfolgreichem Login
die Bestätigung + Grundidentität + die relevanten Keys des Nutzers zurück.

Das Protokoll ist der Standard **OAuth2 Authorization Code Flow** - externe Systeme können
handelsübliche OAuth2-Bibliotheken verwenden.

> **Hinweis:** Dieser SSO-Zugang ist unabhängig von der externen REST-API
> ([API-DOCUMENTATION.md](API-DOCUMENTATION.md), `X-API-Key`) und vom Web-Login der App.

**Basis-URL:** `https://your-domain.com`
**Endpunkte:** `/sso/authorize`, `/sso/token`, `/sso/userinfo`
**Datenformat:** JSON (Token-Endpoint: `application/x-www-form-urlencoded` im Request)

---

## 🧭 Ablauf (Authorization Code Flow)

```
 Externes System            Browser des Nutzers        Feuerwehr Listen
 ---------------            --------------------        ----------------
   1. Redirect  -----------------------------------▶  GET /sso/authorize
                                                        (ggf. Login-Maske)
                                                        Prüft benötigte Keys
   4. code + state ◀-------------------------------  302 Redirect → redirect_uri
   5. POST code+secret -----------------------------▶  POST /sso/token
   6. access_token ◀-------------------------------  { access_token }
   7. GET (Bearer) ---------------------------------▶  GET /sso/userinfo
   8. Identität + keys ◀----------------------------  { sub, username, ..., keys }
```

1. Das externe System leitet den Browser auf `/sso/authorize` weiter.
2. Ist der Nutzer nicht angemeldet, zeigt Feuerwehr Listen die Login-Maske (inkl. QR-Login) und
   kehrt danach automatisch zu `/sso/authorize` zurück.
3. Feuerwehr Listen prüft, ob der Nutzer mindestens einen der vom Client geforderten Keys besitzt.
4. Bei Erfolg: Redirect zur `redirect_uri` mit einem einmaligen `code` (60 s gültig) und dem `state`.
5. Das externe System tauscht `code` + `client_secret` server-seitig gegen ein `access_token`.
6. Mit dem `access_token` liest es `/sso/userinfo` und erhält Identität + gewährte Keys.

---

## ⚙️ Einrichtung (durch Admin)

1. **Berechtigungskeys anlegen:** Web-App → **Verwaltung → Berechtigungskeys**
   (z. B. `alarmmonitor_admin`, `alarmmonitor_user`).
2. **Keys Nutzern zuweisen:** **Verwaltung → Benutzer** → Nutzer bearbeiten → Keys anhaken.
   - Nutzer können mit oder ohne **„Listen-Zugriff"** angelegt werden. Reine SSO-Konten
     (ohne Listen-Zugriff) können sich anmelden und per SSO identifiziert werden, dürfen aber
     das Listen-Tool selbst nicht nutzen.
3. **SSO-Client registrieren:** **Verwaltung → SSO-Clients** → neuen Client anlegen.
   - `client_id` und `client_secret` werden erzeugt; **das Secret wird nur einmalig angezeigt** -
     bitte sicher notieren.
   - **Redirect-URIs** (eine pro Zeile) und **benötigte Keys** hinterlegen.
   - Ist mindestens ein benötigter Key gesetzt, wird der Login nur Nutzern mit passendem Key
     gewährt. Ohne benötigte Keys ist jeder angemeldete Nutzer erlaubt.

---

## 📡 Endpunkte

### 1) Autorisierung

```http
GET /sso/authorize
```

Browser-Weiterleitung (kein direkter API-Aufruf).

| Parameter       | Pflicht | Beschreibung                                                        |
|-----------------|:-------:|---------------------------------------------------------------------|
| `client_id`     | ✅      | Die `client_id` des registrierten SSO-Clients                        |
| `redirect_uri`  | ✅      | Muss **exakt** einer registrierten Redirect-URI entsprechen          |
| `response_type` | ✅      | Muss `code` sein                                                     |
| `state`         | ⭕      | Beliebiger Wert, wird unverändert zurückgegeben (CSRF-Schutz empfohlen) |
| `scope`         | ⭕      | Optional, wird derzeit nicht ausgewertet                             |

**Erfolg** → Redirect an die `redirect_uri`:

```
https://alarmmonitor.example.de/sso/callback?code=98DEDD…B57&state=xyz123
```

**Abgelehnt** (Nutzer hat keinen der benötigten Keys) → Redirect an die `redirect_uri`:

```
https://alarmmonitor.example.de/sso/callback?error=access_denied&state=xyz123
```

**Ungültiger `client_id` / ungültige `redirect_uri`** → `HTTP 400` (kein Redirect,
Open-Redirect-Schutz).

**Nicht angemeldet** → `HTTP 302` zur Login-Maske
(`/login?returnUrl=…`), danach automatische Rückkehr zu `/sso/authorize`.

---

### 2) Token

```http
POST /sso/token
Content-Type: application/x-www-form-urlencoded
```

| Feld            | Pflicht | Beschreibung                          |
|-----------------|:-------:|---------------------------------------|
| `grant_type`    | ✅      | Muss `authorization_code` sein         |
| `code`          | ✅      | Der `code` aus Schritt 1 (einmalig)    |
| `client_id`     | ✅      | `client_id` des Clients                |
| `client_secret` | ✅      | `client_secret` des Clients            |
| `redirect_uri`  | ✅      | Muss der `redirect_uri` aus Schritt 1 entsprechen |

**Erfolg** `HTTP 200`:

```json
{
  "access_token": "808370D1…6D8A",
  "token_type": "Bearer",
  "expires_in": 600,
  "scope": "alarmmonitor_admin"
}
```

> `scope` enthält die dem Nutzer gewährten Keys (leerzeichengetrennt). Das `access_token`
> ist opak und **10 Minuten** gültig.

**Fehler:**

| HTTP | Body                                    | Ursache                                             |
|:----:|-----------------------------------------|-----------------------------------------------------|
| 400  | `{ "error": "invalid_request" }`        | Kein `application/x-www-form-urlencoded`            |
| 400  | `{ "error": "unsupported_grant_type" }` | `grant_type` ≠ `authorization_code`                 |
| 401  | `{ "error": "invalid_client" }`         | Unbekannter/inaktiver Client oder falsches Secret   |
| 400  | `{ "error": "invalid_grant" }`          | Code ungültig/abgelaufen/verbraucht, oder `client_id`/`redirect_uri` passen nicht |

---

### 3) Userinfo

```http
GET /sso/userinfo
Authorization: Bearer <access_token>
```

**Erfolg** `HTTP 200`:

```json
{
  "sub": 1,
  "username": "m.mustermann",
  "firstName": "Max",
  "lastName": "Mustermann",
  "email": "max@feuerwehr.example.de",
  "role": "Admin",
  "keys": ["alarmmonitor_admin"]
}
```

| Feld       | Beschreibung                                             |
|------------|----------------------------------------------------------|
| `sub`      | Stabile, eindeutige Benutzer-ID (Ganzzahl)               |
| `username` | Anmeldename                                              |
| `role`     | Rolle in Feuerwehr Listen (`User` oder `Admin`)          |
| `keys`     | Gewährte Berechtigungskeys (Schnittmenge aus den vom Client geforderten Keys und den Keys des Nutzers; ohne Client-Anforderung: alle Keys des Nutzers) |

**Fehler** `HTTP 401`: `{ "error": "invalid_token" }` (Token fehlt, ungültig oder abgelaufen).

---

## 🧪 Beispiel (curl)

```bash
# Schritt 1 findet im Browser statt und liefert ?code=… an deine redirect_uri.

# Schritt 5: Code gegen Token tauschen (server-seitig)
curl -X POST https://your-domain.com/sso/token \
  -d grant_type=authorization_code \
  -d code=98DEDD…B57 \
  -d client_id=cli_7afcbfae81e26b48 \
  -d client_secret=ab05ac57…31dd \
  -d redirect_uri=https://alarmmonitor.example.de/sso/callback

# Schritt 7: Identität abrufen
curl https://your-domain.com/sso/userinfo \
  -H "Authorization: Bearer 808370D1…6D8A"
```

---

## 🔒 Sicherheitshinweise

- **`client_secret` niemals im Browser** verwenden - der Token-Tausch erfolgt ausschließlich
  server-seitig.
- **`state`** verwenden und nach dem Callback prüfen (CSRF-Schutz).
- **`redirect_uri`** muss exakt registriert sein; abweichende URIs werden mit `400` abgelehnt
  (Open-Redirect-Schutz).
- Client-Secrets werden serverseitig nur **gehasht** (SHA-256) gespeichert und einmalig im
  Klartext angezeigt. Über **Verwaltung → SSO-Clients → Bearbeiten** kann ein neues Secret
  erzeugt werden.
- Auth-Codes sind einmalig und 60 s gültig; Access-Tokens 10 min. Beide werden nur im
  Arbeitsspeicher gehalten - ein Server-Neustart erzwingt lediglich einen erneuten Login.

---

## ❓ Abgrenzung zur REST-API

| | SSO-Provider (`/sso/*`) | REST-API (`/api/*`) |
|---|---|---|
| Zweck | Nutzer-Login + Identität für externe Systeme | Datenzugriff für Maschinen |
| Auth | OAuth2 (client_id/secret, Nutzer-Login) | Statischer `X-API-Key` |
| Verwaltung | Verwaltung → SSO-Clients | Verwaltung → API Keys |

Details zur REST-API: [API-DOCUMENTATION.md](API-DOCUMENTATION.md).
