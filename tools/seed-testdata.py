#!/usr/bin/env python3
"""
Beispieldaten fuer eine TESTUMGEBUNG (Mitgliederbereich, Dienstausweis, Fuehrungskreis).
Laeuft gegen die API einer frisch migrierten Test-Datenbank - nie gegen Produktion!

    python3 tools/seed-testdata.py http://127.0.0.1:5180

Voraussetzungen: Aufruf von 127.0.0.1 (steht auf der Login-Whitelist, noetig fuer die
Passwort-Vergabe), Standard-Admin admin/admin, Python-Pakete requests + Pillow.
"""
import base64
import io
import sys
from datetime import datetime, timedelta

import requests
from PIL import Image, ImageDraw, ImageFont

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5180").rstrip("/")
MEMBER_PASSWORD = "Feuerwehr2026!"

# Nummer, Vorname, Nachname, Dienstgrad, Einheit, Fuehrungskreis (0/1/2), Bereiche, Passwort?, Lichtbild?, Geburtsdatum
MEMBERS = [
    ("1001", "Thomas", "Becker", "Hauptbrandmeister", 1, 2, ["firesafety", "calendar", "attendance", "operations"], True, True, "1978-03-14"),
    ("1002", "Sabine", "Kroll", "Brandmeisterin", 1, 2, ["firesafety", "calendar"], True, True, "1985-09-02"),
    ("1003", "Jan", "Wessels", "Oberbrandmeister", 2, 1, ["firesafety", "calendar"], True, True, "1990-06-21"),
    ("1004", "Anna", "Hölscher", "Unterbrandmeisterin", 2, 1, ["firesafety", "calendar"], True, True, "1993-11-30"),
    ("1005", "Markus", "Brüning", "Löschmeister", 1, 1, ["calendar"], True, True, "1982-01-09"),
    ("1234", "Max", "Mustermann", "Oberfeuerwehrmann", 1, 0, ["firesafety", "calendar"], True, True, "1995-05-17"),
    ("1235", "Erika", "Musterfrau", "Feuerwehrfrau", 2, 0, ["firesafety-link"], True, False, "1999-08-23"),
    ("1236", "Lukas", "Schulte", "Feuerwehrmann", 1, 0, [], False, False, "2004-02-11"),
    ("1237", "Julia", "Terhaar", "Hauptfeuerwehrfrau", 2, 0, ["calendar"], False, False, "1988-12-05"),
    ("1238", "Kevin", "Overbeck", "Feuerwehrmann-Anwärter", 1, 0, [], False, False, "2007-07-07"),
]

COLORS = ["#5B7DB1", "#7A9E7E", "#B5838D", "#6D597A", "#E5989B", "#457B9D", "#9C6644"]


def ok(resp, what):
    if resp.status_code >= 400:
        raise SystemExit(f"FEHLER {what}: {resp.status_code} {resp.text[:300]}")
    return resp


def login(username, password):
    s = requests.Session()
    r = ok(s.post(f"{BASE}/client-api/auth/login", json={"username": username, "password": password}), f"Login {username}").json()
    if not r.get("ok"):
        raise SystemExit(f"Login {username} fehlgeschlagen: {r}")
    ok(s.get(f"{BASE}/client-api/auth/complete", params={"ticket": r["ticket"]}, allow_redirects=False), "complete")
    return s


def portrait(first, last, idx):
    """Platzhalter-Lichtbild 600x750: farbiger Hintergrund, Silhouette, Initialen."""
    img = Image.new("RGB", (600, 750), COLORS[idx % len(COLORS)])
    d = ImageDraw.Draw(img)
    d.ellipse((190, 150, 410, 370), fill="#F1E3D3")
    d.ellipse((90, 420, 510, 900), fill="#2B2D42")
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 90)
    except OSError:
        font = ImageFont.load_default()
    d.text((300, 600), f"{first[0]}{last[0]}", fill="white", font=font, anchor="mm")
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality=85)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


def next_weekday(weekday, hour, minute, weeks=0):
    now = datetime.now()
    days = (weekday - now.weekday()) % 7 or 7
    d = (now + timedelta(days=days + 7 * weeks)).replace(hour=hour, minute=minute, second=0, microsecond=0)
    return d


def iso(d):
    return d.strftime("%Y-%m-%dT%H:%M:%S")


def main():
    admin = login("admin", "admin")

    # --- Einstellungen: Kalender an, Branding, Login-Pflicht mit Whitelist nur lokal ---
    ok(admin.post(f"{BASE}/client-api/admin/settings", json={
        "ModuleVisibility.Calendar": "true",
        "Branding.AppName": "Feuerwehr Musterstadt (TEST)",
        "Security.LoginRequired": "true",
        "Security.LoginWhitelist": "127.0.0.1, ::1",
        "App.BaseUrl": BASE.replace("127.0.0.1", "192.168.2.81"),
        "Card.IssuedBy": "Stadt Musterstadt – Der Bürgermeister\nFreiwillige Feuerwehr Musterstadt",
    }), "Einstellungen")

    # --- Fahrzeuge + Funktion (fuer Wachen) ---
    for name, call in [("HLF 20", "Florian Musterstadt 1-46-1"), ("LF 10", "Florian Musterstadt 2-44-1")]:
        admin.post(f"{BASE}/client-api/admin/vehicles", json={"name": name, "callSign": call, "type": "Löschfahrzeug", "isActive": True})
    vehicles = ok(admin.get(f"{BASE}/client-api/admin/vehicles"), "Fahrzeuge").json()
    functions = ok(admin.get(f"{BASE}/client-api/admin/functions"), "Funktionen").json()
    func_id = functions[0]["id"] if functions else 0

    # --- Mitglieder ---
    existing = {m["memberNumber"]: m for m in ok(admin.get(f"{BASE}/client-api/admin/members"), "Mitglieder").json()}
    for num, first, last, rank, unit, circle, mods, pw, photo, born in MEMBERS:
        if num not in existing:
            ok(admin.post(f"{BASE}/client-api/admin/members", json={
                "memberNumber": num, "firstName": first, "lastName": last, "isActive": True, "units": [unit],
                "birthDate": born + "T00:00:00"}), f"Mitglied {num}")
    members = {m["memberNumber"]: m for m in ok(admin.get(f"{BASE}/client-api/admin/members"), "Mitglieder").json()}

    for num, first, last, rank, unit, circle, mods, pw, photo, born in MEMBERS:
        mid = members[num]["id"]
        ok(admin.put(f"{BASE}/client-api/admin/members/{mid}/account", json={
            "modules": mods, "leadershipCircle": circle, "rank": rank,
            "email": f"{first.lower()}.{last.lower().replace('ö', 'oe').replace('ü', 'ue')}@example.org",
            "cardValidUntil": "2027-12-31T00:00:00"}), f"Zugang {num}")

    # --- Passwort-Vergabe wie am Gerätehaus-Tablet (Admin + Whitelist-Geraet) ---
    ok(admin.post(f"{BASE}/client-api/admin/enrollment/start"), "Vergabe starten")
    for num, first, last, rank, unit, circle, mods, pw, photo, born in MEMBERS:
        if not pw:
            continue
        r = ok(admin.post(f"{BASE}/client-api/admin/enrollment/lookup", json={"code": num}), "lookup").json()
        r = ok(admin.post(f"{BASE}/client-api/admin/enrollment/set", json={"memberId": r["member"]["id"], "password": MEMBER_PASSWORD}), "set").json()
        if not r.get("ok"):
            raise SystemExit(f"Passwort {num}: {r}")
    ok(admin.post(f"{BASE}/client-api/admin/enrollment/stop"), "Vergabe beenden")

    # --- Lichtbilder durch die Mitglieder selbst ---
    sessions = {}
    for i, (num, first, last, rank, unit, circle, mods, pw, photo, born) in enumerate(MEMBERS):
        if not pw:
            continue
        sessions[num] = login(num, MEMBER_PASSWORD)
        if photo:
            r = ok(sessions[num].post(f"{BASE}/client-api/me/photo", json={"dataUrl": portrait(first, last, i)}), f"Foto {num}").json()
            if not r.get("ok"):
                raise SystemExit(f"Foto {num}: {r}")

    # --- Brandsicherheitswachen ---
    for name, loc, days, amount in [("Weihnachtsmarkt", "Marktplatz", 10, 4), ("Martinsumzug", "Kirchplatz", 4, 2)]:
        start = (datetime.now() + timedelta(days=days)).replace(hour=16, minute=0, second=0, microsecond=0)
        admin.post(f"{BASE}/client-api/admin/firesafetywatches", json={
            "name": name, "location": loc, "eventTime": iso(start), "eventEndTime": iso(start + timedelta(hours=5)),
            "requirements": [{"functionDefId": func_id, "amount": amount, "vehicleId": vehicles[0]["id"] if vehicles else None}]})

    # --- Kalender: Dienste + Fuehrungskreis-Sitzungen ---
    def event(sess, **body):
        body.setdefault("allDay", False)
        body.setdefault("vehicleIds", [])
        body.setdefault("roomIds", [])
        r = ok(sess.post(f"{BASE}/client-api/calendar/events", json=body), f"Termin {body['title']}").json()
        if r.get("status") != "ok":
            raise SystemExit(f"Termin {body['title']}: {r}")
        return r["id"]

    d1 = next_weekday(3, 19, 0)  # Donnerstag
    event(admin, type="Dienst", title="Übungsdienst Löschzug 1", location="Gerätehaus", start=iso(d1), end=iso(d1 + timedelta(hours=2)),
          unitNumber=1, requestedBy="admin")
    d2 = next_weekday(0, 19, 30)  # Montag
    event(admin, type="Dienst", title="Atemschutz-Übung", location="Übungsstrecke", start=iso(d2), end=iso(d2 + timedelta(hours=2)),
          unitNumber=2, requestedBy="admin")

    becker = sessions["1001"]  # kleiner Kreis
    past = (datetime.now() - timedelta(days=14)).replace(hour=19, minute=30, second=0, microsecond=0)
    past_id = event(becker, type="Fuehrungskreis", leadershipCircle=1, title="Sitzung Großer Führungskreis", location="Schulungsraum",
                    start=iso(past), end=iso(past + timedelta(hours=2)))
    nxt = next_weekday(1, 19, 30)  # Dienstag
    event(becker, type="Fuehrungskreis", leadershipCircle=1, title="Sitzung Großer Führungskreis", location="Schulungsraum",
          start=iso(nxt), end=iso(nxt + timedelta(hours=2)))
    small = next_weekday(4, 18, 0)  # Freitag
    event(becker, type="Fuehrungskreis", leadershipCircle=2, title="Sitzung Kleiner Führungskreis", location="Büro Wehrführung",
          start=iso(small), end=iso(small + timedelta(hours=1, minutes=30)))

    # --- Vergangene Sitzung durchspielen: Punkte, Notizen, Aufgaben, Anwesenheit, Abschluss ---
    pid = ok(becker.get(f"{BASE}/client-api/leadership/protocols/by-event/{past_id}"), "Protokoll").json()["id"]
    for title, owner in [("Bericht der Wehrführung", "1001"), ("Übungsobjekt für Q1", "1002"), ("Beschaffung C-Schläuche", "1003")]:
        becker.post(f"{BASE}/client-api/leadership/protocols/{pid}/items", json={"title": title, "ownerMemberId": members[owner]["id"]})
    ok(becker.post(f"{BASE}/client-api/leadership/protocols/{pid}/start"), "Sitzung starten")
    proto = becker.get(f"{BASE}/client-api/leadership/protocols/{pid}").json()
    states = {"1001": 1, "1002": 1, "1003": 1, "1004": 2, "1005": 0}
    for a in proto["attendance"]:
        num = next(n for n, m in members.items() if m["id"] == a["memberId"])
        becker.post(f"{BASE}/client-api/leadership/protocols/{pid}/attendance", json={"memberId": a["memberId"], "state": states.get(num, 1)})
    notes = {
        "Bericht der Wehrführung": ("Einsatzzahlen bis Oktober: 87. Neues Mitglied in Löschzug 2.", "", "Besprochen"),
        "Übungsobjekt für Q1": ("Hof Schulze angefragt, Rückmeldung steht aus.", "", "Besprochen"),
        "Beschaffung C-Schläuche": ("Zwei Angebote liegen vor, drittes fehlt noch.", "Beschaffung nach drittem Angebot.", "Besprochen"),
    }
    item_ids = {}
    for it in proto["items"]:
        item_ids[it["title"]] = it["id"]
        if it["title"] in notes:
            n, dec, st = notes[it["title"]]
            becker.put(f"{BASE}/client-api/leadership/items/{it['id']}", json={
                "title": it["title"], "ownerMemberId": it["ownerId"], "notes": n, "decision": dec, "status": st})
    task_defs = [
        ("Drittes Angebot C-Schläuche einholen", "1003", 6, 1, item_ids["Beschaffung C-Schläuche"]),
        ("Übungsobjekt Q1 klären", "1002", -3, 1, item_ids["Übungsobjekt für Q1"]),
        ("Dienstplan 2027 entwerfen", "1001", 23, 2, None),
        ("Atemschutz-Prüffristen nachhalten", "1004", -2, 1, None),
        ("Jahresbericht Jugendfeuerwehr anfordern", "1005", 38, 1, None),
    ]
    task_ids = {}
    for title, who, due_days, circle, item in task_defs:
        r = ok(becker.post(f"{BASE}/client-api/leadership/tasks", json={
            "title": title, "circle": circle, "assigneeMemberId": members[who]["id"],
            "dueDate": iso(datetime.now() + timedelta(days=due_days)), "sourceItemId": item}), f"Aufgabe {title}").json()
        task_ids[title] = r["id"]
    ok(becker.post(f"{BASE}/client-api/leadership/protocols/{pid}/close"), "Sitzung abschliessen")

    # Erledigte Aufgabe -> landet automatisch als Punkt in der naechsten Sitzung des grossen Kreises.
    ok(sessions["1002"].post(f"{BASE}/client-api/leadership/tasks/{task_ids['Übungsobjekt Q1 klären']}/done"), "Aufgabe erledigt")

    print("Beispieldaten angelegt.")
    print(f"  Admin:     admin / admin")
    print(f"  Mitglieder: Nummer + {MEMBER_PASSWORD}  (1001 kleiner FK, 1003 grosser FK, 1234 ohne FK, 1235 ohne Lichtbild)")


if __name__ == "__main__":
    main()
