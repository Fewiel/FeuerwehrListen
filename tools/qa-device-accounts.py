#!/usr/bin/env python3
# QA Geraetekonten (API): Login, Kiosk-Rechte, Mitglied obendrauf, Passwort-Vergabe, Sperren.
import requests, time
LAN="http://192.168.2.81:5180"; LOCAL="http://127.0.0.1:5180"; PW="Feuerwehr2026!"
fails=[]
def check(n,c,i=""):
    print(("PASS " if c else "FAIL ")+n+("" if c else f"  -> {i}"))
    if not c: fails.append(n)
def login(base,u,p):
    s=requests.Session(); r=s.post(base+"/client-api/auth/login",json={"username":u,"password":p}).json(); assert r.get("ok"),(u,r)
    s.get(base+"/client-api/auth/complete",params={"ticket":r["ticket"]},allow_redirects=False); return s,r
ad,_=login(LAN,"admin","admin")
users={u["username"]:u for u in ad.get(LAN+"/client-api/admin/users").json()}
for name,first,mods,enroll in [("elw1","ELW 1",["attendance","operations","firesafety","calendar"],False),("tablet-gh","Tablet Gerätehaus",["attendance","operations","firesafety","defects","calendar"],True)]:
    if name not in users:
        r=ad.post(LAN+"/client-api/admin/users",json={"username":name,"firstName":first,"lastName":"","role":"Device","password":"Geraet2026!","modules":mods,"fullAccess":False,"allowEnrollment":enroll,"hasListAccess":False}); assert r.ok, r.text
users={u["username"]:u for u in ad.get(LAN+"/client-api/admin/users").json()}
check("Geraetekonten angelegt (Rolle Device, kein Vollzugang)", users["elw1"]["role"]=="Device" and users["elw1"]["fullAccess"] is False and users["tablet-gh"]["allowEnrollment"] is True, users.get("elw1"))

print("== ELW-iPad von aussen (LAN-IP, wechselnd) ==")
elw,r=login(LAN,"elw1","Geraet2026!")
check("Login liefert device-Ticket", r.get("device") is True, r)
check("Geraete-Cookie gesetzt, KEIN Benutzer-Cookie", "fw_device" in elw.cookies and "fw_auth" not in elw.cookies, dict(elw.cookies))
ctx=elw.get(LAN+"/client-api/app-context").json()
check("app-context: Geraet erkannt, kein Login noetig, nur Geraete-Module", ctx["deviceName"]=="ELW 1" and ctx["loginRequired"] is False and ctx["modules"]["attendance"] and ctx["modules"]["operations"] and not ctx["modules"]["defects"] and not ctx["modules"]["leadership"], ctx)
r=elw.get(LAN+"/",headers={"Accept":"text/html"},allow_redirects=False); check("Startseite ohne Login-Umleitung", r.status_code==200, r.status_code)
check("open-lists erreichbar", elw.get(LAN+"/client-api/open-lists").status_code==200)
check("Wachen erreichbar", elw.get(LAN+"/client-api/firesafetywatches").status_code==200)
check("Maengel (nicht freigegeben) -> 403", elw.get(LAN+"/client-api/defects").status_code==403)
check("auth/me: Geraet ist NICHT angemeldet", elw.get(LAN+"/client-api/auth/me").json()=={"authenticated":False})
wid=elw.get(LAN+"/client-api/firesafetywatches").json()[0]["id"]
check("Geraet: Wache schliessen -> 401 (keine Verwaltungsrechte)", elw.post(LAN+f"/client-api/firesafetywatch/{wid}/close").status_code==401)
check("Geraet: Admin-API -> 401", elw.get(LAN+"/client-api/admin/members").status_code==401)
check("Geraet: Fuehrungskreis gesperrt", elw.get(LAN+"/client-api/leadership/tasks").status_code in (401,403))
check("Geraet: externe /api -> 401", elw.get(LAN+"/api/members").status_code==401)
check("Geraet: Mitgliederbereich -> 401", elw.get(LAN+"/client-api/me").status_code==401)
r=elw.post(LAN+"/client-api/attendance/create",json={"title":"ELW-Test","unit":"Allgemein"}); check("Geraet darf Anwesenheitsliste anlegen (Kiosk)", r.status_code==200, r.status_code)
# Mitglied obendrauf
r=elw.post(LAN+"/client-api/auth/login",json={"username":"1234","password":PW}).json(); elw.get(LAN+"/client-api/auth/complete",params={"ticket":r["ticket"]},allow_redirects=False)
me=elw.get(LAN+"/client-api/me").json(); check("Mitglied auf dem Geraet angemeldet", me.get("isMember") and me["firstName"]=="Max", me)
ctx=elw.get(LAN+"/client-api/app-context").json(); check("mit Mitglied: Module des Mitglieds (Kalender ja, Anwesenheit nein)", ctx["modules"]["calendar"] and not ctx["modules"]["attendance"], ctx["modules"])
elw.get(LAN+"/client-api/auth/logout-redirect",allow_redirects=False)
ctx=elw.get(LAN+"/client-api/app-context").json(); check("nach Abmelden: zurueck im Geraete-Zustand", ctx["deviceName"]=="ELW 1" and ctx["loginRequired"] is False and ctx["modules"]["attendance"], ctx)
# Sperren
users={u["username"]:u for u in ad.get(LAN+"/client-api/admin/users").json()}
check("Verwaltung zeigt 1 angemeldetes Geraet", users["elw1"]["deviceCount"]==1 and users["elw1"]["deviceLastIp"], users["elw1"])
# Passwort-Vergabe: elw1 nicht erlaubt, tablet-gh erlaubt
tab,_=login(LAN,"tablet-gh","Geraet2026!")
r=tab.post(LAN+"/client-api/auth/login",json={"username":"admin","password":"admin"}).json(); tab.get(LAN+"/client-api/auth/complete",params={"ticket":r["ticket"]},allow_redirects=False)
st=tab.get(LAN+"/client-api/admin/enrollment").json(); check("Admin auf Tablet-Geraet: Vergabe erlaubt", st["allowedDevice"] is True and st["deviceName"]=="Tablet Gerätehaus", st)
r=tab.post(LAN+"/client-api/admin/enrollment/start").json(); check("Vergabe-Modus auf Geraetekonto startet (LAN-IP!)", r.get("ok") is True, r)
r=tab.post(LAN+"/client-api/admin/enrollment/lookup",json={"code":"1237"}).json(); check("Vergabe: Mitglied gefunden", r.get("ok") and r["member"]["name"]=="Julia Terhaar", r)
r=tab.post(LAN+"/client-api/admin/enrollment/set",json={"memberId":r["member"]["id"],"password":"TerhaarTest1!"}).json(); check("Vergabe: Passwort gesetzt", r.get("ok") is True, r)
tab.post(LAN+"/client-api/admin/enrollment/stop")
r=elw.post(LAN+"/client-api/auth/login",json={"username":"admin","password":"admin"}).json(); elw.get(LAN+"/client-api/auth/complete",params={"ticket":r["ticket"]},allow_redirects=False)
st=elw.get(LAN+"/client-api/admin/enrollment").json(); check("Admin auf ELW-Geraet: Vergabe NICHT erlaubt", st["allowedDevice"] is False, st)
r=elw.post(LAN+"/client-api/admin/enrollment/start").json(); check("Vergabe auf ELW abgelehnt", r.get("error")=="device", r)
elw.get(LAN+"/client-api/auth/logout-redirect",allow_redirects=False)
# Alle Geraete abmelden
ad.post(LAN+f"/client-api/admin/users/{users['elw1']['id']}/devices/logout-all")
ctx=elw.get(LAN+"/client-api/app-context").json(); check("nach 'Alle Geraete abmelden': Geraet sofort gesperrt", ctx["deviceName"] is None and ctx["loginRequired"] is True, ctx)
check("open-lists danach 401", elw.get(LAN+"/client-api/open-lists").status_code==401)
# Passwortwechsel wirft Tablet raus
u=users["tablet-gh"]; ad.put(LAN+f"/client-api/admin/users/{u['id']}",json={"username":"tablet-gh","firstName":"Tablet Gerätehaus","lastName":"","role":"Device","password":"Geraet2026!","modules":["attendance","operations","firesafety","defects","calendar"],"allowEnrollment":True})
ctx=tab.get(LAN+"/client-api/app-context").json(); check("Passwortwechsel meldet Tablet ab (Admin-Cookie noch da -> angemeldet, aber Geraet weg)", ctx["deviceName"] is None, ctx)
tab.get(LAN+"/client-api/auth/logout-redirect",allow_redirects=False); check("Tablet ohne Admin: Login noetig", tab.get(LAN+"/client-api/open-lists").status_code==401)
# Geraet kann keinen Benutzer-Cookie bekommen (QR-Login ausgeschlossen)
r=requests.post(LAN+"/client-api/auth/qr",json={"code":"x"}).json(); check("QR-Login fuer Geraete nicht moeglich", r["status"]=="invalid")
# gefaelschtes Geraete-Cookie
r=requests.get(LAN+"/client-api/open-lists",cookies={"fw_device":"deadbeef"*8}); check("gefaelschtes Geraete-Cookie -> 401", r.status_code==401)
print("\nFEHLER:", fails if fails else "keine")
