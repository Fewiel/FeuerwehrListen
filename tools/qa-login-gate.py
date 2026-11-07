#!/usr/bin/env python3
# QA: Login-Pflicht, Mitglieder-Grenzen, Fuehrungskreis, Pruef-Links - gegen die Testumgebung (LAN-IP = nicht gewhitelistet, 127.0.0.1 = Whitelist).
import requests, json, sys
LAN="http://192.168.2.81:5180"; LOCAL="http://127.0.0.1:5180"; PW="Feuerwehr2026!"
fails=[]
def check(name, cond, info=""):
    print(("PASS " if cond else "FAIL ")+name+("" if cond else f"  -> {info}"))
    if not cond: fails.append(name)
def login(base,u,p):
    s=requests.Session(); r=s.post(base+"/client-api/auth/login",json={"username":u,"password":p}).json()
    assert r.get("ok"), (u,r)
    s.get(base+"/client-api/auth/complete",params={"ticket":r["ticket"]},allow_redirects=False); return s
A=requests.Session()
H={"Accept":"text/html"}
print("== Anonym von LAN-IP (nicht gewhitelistet) ==")
for p in ["/","/kalender","/mein","/admin/users","/fuehrungskreis","/firesafetywatches/5"]:
    r=A.get(LAN+p,headers=H,allow_redirects=False); check(f"Seite {p} -> Login", r.status_code==302 and "/login" in r.headers.get("location",""), r.status_code)
r=A.get(LAN+"/kalender",headers={"Accept":"*/*"},allow_redirects=False); check("Seite /kalender mit Accept */* -> Login", r.status_code==302, r.status_code)
r=A.get(LAN+"/login",headers=H); check("/login erreichbar", r.status_code==200, r.status_code)
for p in ["/client-api/open-lists","/client-api/me","/client-api/firesafetywatches","/client-api/firesafetywatch/5","/client-api/export/attendance/1/pdf",
          "/client-api/leadership/tasks","/client-api/auth/qr-identify?code=abc","/client-api/calendar","/client-api/members/search?q=a","/token?path=/api/export/members/pdf",
          "/client-api/admin/members","/client-api/closed-lists","/client-api/me/photo"]:
    r=A.get(LAN+p); check(f"API {p} -> 401", r.status_code==401, r.status_code)
r=A.post(LAN+"/client-api/attendance/create",json={"title":"x","unit":"y"}); check("POST attendance/create -> 401", r.status_code==401, r.status_code)
r=A.get(LAN+"/client-api/app-context").json(); check("app-context: alle Module aus, keine Einheiten, loginRequired", r["loginRequired"] and not any(v for k,v in r["modules"].items() if k!="fullAccess") and r["unitLabels"]==[], r)
r=A.get(LAN+"/client-api/auth/me").json(); check("auth/me anonym", r=={"authenticated":False}, r)
r=A.get(LAN+"/api/members"); check("/api ohne Key -> 401", r.status_code==401, r.status_code)
r=A.get(LAN+"/api/export/members/pdf?token=abc"); check("/api/export mit falschem Token", r.status_code in (401,403,404), r.status_code)
r=A.get(LAN+"/sso/userinfo"); check("/sso/userinfo ohne Token -> 401", r.status_code==401, r.status_code)
r=A.get(LAN+"/sso/authorize?client_id=x&redirect_uri=http://x"); check("/sso/authorize unbekannter Client -> 400", r.status_code==400, r.status_code)
r=A.get(LAN+"/swagger/index.html"); print(f"INFO /swagger: {r.status_code} (nur im Development-Modus offen; Produktion = 404)")
# Spoofing
r=A.get(LAN+"/client-api/open-lists",headers={"X-Forwarded-For":"127.0.0.1"}); check("X-Forwarded-For 127.0.0.1 hilft nicht", r.status_code==401, r.status_code)
r=A.get(LAN+"/client-api/open-lists",headers={"X-Fw-Internal":"deadbeef","X-Fw-Client-Ip":"127.0.0.1"}); check("gefaelschte interne Header helfen nicht", r.status_code==401, r.status_code)
r=A.get(LAN+"/client-api/open-lists",headers={"X-Fw-User":"admin","X-Fw-Role":"Admin"}); check("X-Fw-User ohne Geheimnis hilft nicht", r.status_code==401, r.status_code)
r=A.get(LAN+"/client-api/auth/complete?ticket=abc",allow_redirects=False); check("complete mit falschem Ticket -> /login", r.headers.get("location")=="/login", r.headers.get("location"))
r=A.post(LAN+"/client-api/auth/login",json={"username":"1234","password":"falsch"}).json(); check("falsches Passwort abgelehnt", r.get("ok") is False, r)
r=A.post(LAN+"/client-api/auth/login",json={"username":"1236","password":PW}).json(); check("Mitglied ohne Passwort kann sich nicht anmelden", r.get("ok") is False, r)
# oeffentliche Pruefung
m=login(LAN,"1234",PW); me=m.get(LAN+"/client-api/me").json(); cid=me["card"]["checkUrl"].rsplit("/",1)[1]
r=A.get(LAN+f"/client-api/card-check/{cid}").json(); check("Pruef-Link gueltig ohne Login", r=={"valid":True,"checkedAt":r.get("checkedAt")} and set(r)=={"valid","checkedAt"}, r)
r=A.get(LAN+f"/client-api/card-check/{cid}/photo"); check("Pruef-Foto ohne Login", r.status_code==200 and r.headers["content-type"]=="image/jpeg", r.status_code)
r=A.get(LAN+f"/ausweis/{cid}",headers=H); check("Pruefseite ohne Login erreichbar", r.status_code==200, r.status_code)
r=A.get(LAN+"/client-api/card-check/00000000000000000000000000000000").json(); check("unbekannter Pruef-Link -> valid:false ohne Daten", r=={"valid":False,"checkedAt":r.get("checkedAt")}, r)
r=A.get(LAN+"/client-api/card-check/00000000000000000000000000000000/photo"); check("Foto unbekannter Link -> 404", r.status_code==404, r.status_code)
r=A.get(LAN+f"/client-api/firesafetywatch-link/abc"); check("Wachen-Direktlink braucht Login (Gate)", r.status_code==401, r.status_code)
print("== Mitglied 1234 (Wachen+Kalender) ==")
check("me: Geburtsdatum + Aussteller", me.get("birthDate","").startswith("1995-05-17") and "Bürgermeister" in (me.get("issuedBy") or ""), (me.get("birthDate"),me.get("issuedBy")))
r=m.get(LAN+"/api/statistics/overview"); check("Mitglied: externe /api gesperrt", r.status_code==401, r.status_code)
r=m.get(LAN+"/token?path=/api/export/members/pdf"); check("Mitglied: kein Export-Token", r.status_code==403, r.status_code)
r=m.post(LAN+"/client-api/firesafetywatch/5/close"); check("Mitglied: Wache schliessen -> 403", r.status_code==403, r.status_code)
r=m.get(LAN+"/client-api/attendance/1/excuse-candidates"); check("Mitglied: Entschuldigt-Liste -> 403", r.status_code==403, r.status_code)
for p in ["/client-api/closed-lists","/client-api/admin/members","/client-api/leadership/tasks","/client-api/attendance/1","/client-api/defects","/client-api/operations/recent"]:
    r=m.get(LAN+p); check(f"Mitglied: {p} -> 403", r.status_code==403, r.status_code)
r=m.post(LAN+"/client-api/calendar/series",json={"title":"x"}); check("Mitglied: Serie anlegen -> 403", r.status_code==403, r.status_code)
for p in ["/client-api/calendar","/client-api/firesafetywatches","/client-api/me/card-qr","/client-api/me/overview"]:
    r=m.get(LAN+p); check(f"Mitglied: {p} erlaubt", r.status_code==200, r.status_code)
r=m.get(LAN+"/create-attendance",headers=H,allow_redirects=False); check("Mitglied: gesperrte Seite -> Uebersicht", r.status_code==302, r.status_code)
r=m.get(LAN+"/sso/authorize?client_id=x&redirect_uri=x"); check("Mitglied: SSO unbekannter Client -> 400", r.status_code==400, r.status_code)
print("== Fuehrungskreis ==")
k=login(LAN,"1001",PW); g=login(LAN,"1003",PW)
pk=k.get(LAN+"/client-api/leadership/protocols").json(); small=[p for p in pk["upcoming"] if p["circle"]==2][0]
r=g.get(LAN+f"/client-api/leadership/protocols/{small['id']}"); check("grosser Kreis sieht kleine Sitzung nicht", r.status_code==404, r.status_code)
r=g.get(LAN+f"/client-api/leadership/protocols/{small['id']}/pdf"); check("grosser Kreis: kleines PDF -> 404", r.status_code==404, r.status_code)
r=g.post(LAN+f"/client-api/leadership/protocols/{small['id']}/items",json={"title":"x"}); check("grosser Kreis: Punkt in kleiner Sitzung -> 404", r.status_code==404, r.status_code)
r=g.post(LAN+"/client-api/leadership/tasks",json={"title":"x","circle":2}); check("grosser Kreis: Aufgabe fuer kleinen Kreis -> 403", r.status_code==403, r.status_code)
r=k.get(LAN+f"/client-api/leadership/protocols/{small['id']}"); check("kleiner Kreis sieht kleine Sitzung", r.status_code==200, r.status_code)
ev=[e for e in g.get(LAN+"/client-api/calendar").json()["items"] if e["kind"]=="fuehrungskreis"]; check("grosser Kreis: nur grosse Sitzungen im Kalender", ev and all(e["leadershipCircle"]==1 for e in ev), [e["title"] for e in ev])
r=g.post(LAN+"/client-api/calendar/events",json={"type":"Fuehrungskreis","leadershipCircle":2,"title":"x","start":"2027-01-01T10:00:00","end":"2027-01-01T11:00:00","allDay":False,"vehicleIds":[],"roomIds":[]}).json(); check("grosser Kreis: kleine Sitzung anlegen abgelehnt", r.get("status")=="invalid", r)
print("== Admin ==")
ad=login(LAN,"admin","admin")
r=ad.get(LAN+"/api/members"); check("Admin: /api ohne Key weiterhin", r.status_code==200, r.status_code)
r=ad.post(LAN+"/client-api/admin/enrollment/start").json(); check("Admin von LAN: Passwort-Vergabe verweigert (kein Whitelist-Geraet)", r.get("error")=="device", r)
al=login(LOCAL,"admin","admin"); r=al.post(LOCAL+"/client-api/admin/enrollment/start").json(); check("Admin von Whitelist-IP: Vergabe startet", r.get("ok") is True, r)
r=al.post(LOCAL+"/client-api/admin/enrollment/lookup",json={"code":"1236"}).json(); check("Vergabe: Mitglied mit Geburtsdatum gefunden", r.get("ok") and r["member"]["birthDate"].startswith("2004-02-11"), r)
r=ad.post(LAN+"/client-api/admin/enrollment/lookup",json={"code":"1236"}).json(); check("Vergabe-Sitzung gilt nicht von anderer IP", r.get("error")=="session", r)
al.post(LOCAL+"/client-api/admin/enrollment/stop")
print("\nFEHLER:", fails if fails else "keine")
