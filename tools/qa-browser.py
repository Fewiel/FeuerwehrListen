#!/usr/bin/env python3
# QA im Browser (Playwright): Ausweis, Einstellungen, Passwort-Vergabe, Server-Modus. Aufruf: python3 tools/qa-browser.py <Screenshot-Ordner>
import json, sys
from playwright.sync_api import sync_playwright
LAN='http://192.168.2.81:5180'; LOCAL='http://127.0.0.1:5180'; PW='Feuerwehr2026!'
OUT=sys.argv[1]; errors=[]; results=[]
def res(name, ok, info=''): results.append(('PASS ' if ok else 'FAIL ')+name+('' if ok else f' -> {info}'))
with sync_playwright() as p:
    br=p.chromium.launch()
    def ctx_for(base,user,pw,opts):
        c=br.new_context(**opts)
        if user:
            j=c.request.post(base+'/client-api/auth/login',data={'username':user,'password':pw}).json()
            c.request.get(base+'/client-api/auth/complete?ticket='+j['ticket'],max_redirects=0)
        return c
    def page(c,url,name,wait=2500,until='networkidle'):
        pg=c.new_page()
        pg.on('pageerror',lambda e: errors.append(f'{name}: {e}'))
        pg.on('console',lambda m: errors.append(f'{name} console: {m.text[:200]}') if m.type=='error' else None)
        pg.goto(url,wait_until=until); pg.wait_for_timeout(wait)
        pg.screenshot(path=f'{OUT}/{name}.png',full_page=True)
        return pg
    phone={**p.devices['iPhone 13'],'device_scale_factor':1}
    tab={'viewport':{'width':1180,'height':820},'device_scale_factor':1}
    # Ausweis mit Geburtsdatum + Aussteller
    m=ctx_for(LAN,'1234',PW,phone); pg=page(m,LAN+'/mein/ausweis','q1-ausweis'); t=pg.inner_text('body')
    res('Ausweis zeigt Geburtsdatum + Aussteller','17.05.1995' in t and 'Bürgermeister' in t, t[:300]); pg.close()
    # Einstellungen + Mitgliederformular (Admin)
    ad=ctx_for(LAN,'admin','admin',tab); pg=page(ad,LAN+'/admin/settings','q2-settings'); res('Einstellungen: Feld Ausgestellt von','Ausgestellt von' in pg.inner_text('body')); pg.close()
    pg=page(ad,LAN+'/admin/members','q3-members'); pg.get_by_role('button',name='Bearbeiten').first.click(); pg.wait_for_timeout(800)
    res('Mitglied bearbeiten: Geburtsdatum-Feld', pg.locator('input[type=date]').count()>=1); pg.screenshot(path=f'{OUT}/q3-members-edit.png',full_page=True); pg.close()
    # Passwort-Vergabe am Whitelist-Geraet (127.0.0.1)
    al=ctx_for(LOCAL,'admin','admin',tab); pg=page(al,LOCAL+'/admin/passwort-vergabe','q4-vergabe-admin')
    pg.get_by_role('button',name='Vergabe-Modus starten').click(); pg.wait_for_url('**/passwort-vergabe',timeout=10000); pg.wait_for_timeout(1500)
    pg.fill('#mnr','1236'); pg.get_by_role('button',name='Weiter').click(); pg.wait_for_timeout(1500)
    t=pg.inner_text('body'); res('Vergabe: Mitglied erkannt mit Geburtsdatum','Lukas Schulte' in t and '11.02.2004' in t, t[:300])
    pg.screenshot(path=f'{OUT}/q5-vergabe-schritt2.png',full_page=True)
    pg.fill('#p1','TestPasswort1!'); pg.fill('#p2','TestPasswort1!'); pg.get_by_role('button',name='Passwort festlegen',exact=True).click(); pg.wait_for_timeout(1500)
    res('Vergabe: Passwort gesetzt','Fertig!' in pg.inner_text('body')); pg.screenshot(path=f'{OUT}/q6-vergabe-fertig.png',full_page=True); pg.close()
    j=br.new_context().request.post(LAN+'/client-api/auth/login',data={'username':'1236','password':'TestPasswort1!'}).json(); res('1236 kann sich danach von aussen anmelden', j.get('ok') is True, j)
    al.request.post(LOCAL+'/client-api/admin/enrollment/stop')
    # Server-Modus (alte Geraete): Kiosk von Whitelist-IP anonym
    k=br.new_context(**tab); pg=page(k,LOCAL+'/?rmode=server','q7-server-kiosk',6000,'load'); t=pg.inner_text('body')
    res('Server-Modus Kiosk (Whitelist-IP): offene Listen sichtbar, kein Login','Martinsumzug' in t and 'Login' in t, t[:300]); pg.close()
    # Server-Modus anonym von aussen -> Login
    a=br.new_context(**tab); pg=a.new_page(); pg.goto(LAN+'/kalender?rmode=server',wait_until='load'); pg.wait_for_timeout(3000); res('Server-Modus von aussen anonym -> Login','/login' in pg.url, pg.url); pg.close()
    # Server-Modus Mitglied von aussen
    ms=ctx_for(LAN,'1234',PW,tab); pg=page(ms,LAN+'/mein?rmode=server','q8-server-mitglied',6000,'load'); t=pg.inner_text('body')
    res('Server-Modus Mitglied: Mein Bereich laedt','Hallo, Max' in t and 'Martinsumzug' in t, t[:300]); pg.close()
    # Mitglied ohne Lichtbild
    e=ctx_for(LAN,'1235',PW,phone); pg=page(e,LAN+'/mein/ausweis','q9-ausweis-ohne-foto'); res('Ausweis ohne Lichtbild: Hinweis','Lichtbild' in pg.inner_text('body')); pg.close()
    br.close()
print('\n'.join(results)); print('JS-ERRORS:', json.dumps(errors,indent=1,ensure_ascii=False))
