#!/usr/bin/env python3
# QA Geraetekonten im Browser (WASM + iOS-12-Server-Modus) und Verwaltung.
from playwright.sync_api import sync_playwright
LAN='http://192.168.2.81:5180'
UA='Mozilla/5.0 (iPad; CPU OS 12_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/12.1.2 Mobile/15E148 Safari/604.1'
res=[]; errs=[]
def check(n,c,i=''): res.append(('PASS ' if c else 'FAIL ')+n+('' if c else f' -> {i}'))
with sync_playwright() as p:
    br=p.chromium.launch()
    def ui_login(ctx,user,pw,until='networkidle',wait=2500):
        pg=ctx.new_page(); pg.on('pageerror',lambda e: errs.append(str(e)))
        pg.goto(LAN+'/login',wait_until=until); pg.wait_for_timeout(wait)
        pg.fill('input[autocomplete=username]',user); pg.fill('input[type=password]',pw)
        pg.get_by_role('button',name='Anmelden',exact=True).click(); pg.wait_for_url(LAN+'/**',timeout=15000); pg.wait_for_timeout(wait+2000)
        return pg
    # WASM (modernes Geraet)
    c=br.new_context(viewport={'width':1180,'height':820})
    pg=ui_login(c,'elw1','Geraet2026!'); t=pg.inner_text('body')
    check('WASM: Geraete-Login landet auf Uebersicht mit Listen', pg.url.rstrip('/')==LAN and 'Martinsumzug' in t, pg.url)
    check('WASM: Kopfzeile zeigt Geraetename + Anmelden', 'ELW 1' in pg.locator('.tw-appbar').inner_text() and 'Anmelden' in pg.locator('.tw-appbar').inner_text(), pg.locator('.tw-appbar').inner_text())
    check('WASM: Maengel nicht im Menue (nicht freigegeben)', 'Mängel' not in pg.locator('.tw-nav').inner_text(), pg.locator('.tw-nav').inner_text())
    pg.screenshot(path='shots/dev1-wasm-kiosk.png',full_page=True)
    pg.goto(LAN+'/kalender',wait_until='networkidle'); pg.wait_for_timeout(2500); check('WASM: Kalender ohne Login', '/login' not in pg.url and 'Kalender' in pg.inner_text('h1'), pg.url)
    pg.goto(LAN+'/defects',wait_until='networkidle'); pg.wait_for_timeout(2500); check('WASM: Maengel-Seite gesperrt', 'Kein Zugriff' in pg.inner_text('body') or 'nicht verfügbar' in pg.inner_text('body').lower(), pg.inner_text('body')[:200])
    # Alte iPads (Server-Modus) - eigener Context = neues Geraet
    s=br.new_context(user_agent=UA,viewport={'width':1024,'height':768})
    pg=ui_login(s,'elw1','Geraet2026!',until='load',wait=4000); t=pg.inner_text('body')
    check('Server-Modus: Geraete-Login -> Uebersicht mit Listen', 'Martinsumzug' in t and pg.locator('#fw-heartbeat').count()>0, (pg.url, t[:200]))
    check('Server-Modus: Kopfzeile zeigt Geraetename', 'ELW 1' in pg.locator('.tw-appbar').inner_text(), pg.locator('.tw-appbar').inner_text())
    pg.screenshot(path='shots/dev2-ipad-kiosk.png',full_page=True)
    pg.goto(LAN+'/firesafetywatches',wait_until='load'); pg.wait_for_timeout(5000); check('Server-Modus: Wachen-Liste laedt (Circuit mit Geraete-Token)', 'Martinsumzug' in pg.inner_text('body') and '/login' not in pg.url, pg.url)
    # Verwaltung: Geraete in der Benutzerliste
    a=br.new_context(viewport={'width':1180,'height':820})
    pg=ui_login(a,'admin','admin'); pg.goto(LAN+'/admin/users',wait_until='networkidle'); pg.wait_for_timeout(2500); t=pg.inner_text('body')
    check('Verwaltung: Geraetekonten mit Anzahl angemeldeter Geraete', 'Gerät' in t and 'angemeldet' in t and 'Alle Geräte abmelden' in t, t[:300])
    pg.screenshot(path='shots/dev3-admin-users.png',full_page=True)
    pg.get_by_role('button',name='Neuer Benutzer').click(); pg.wait_for_timeout(500); pg.select_option('select.form-select >> nth=0','Device'); pg.wait_for_timeout(500)
    check('Formular Rolle Geraet: Bereiche + Passwort-Vergabe-Schalter', 'Bereiche auf dem Gerät' in pg.inner_text('body') and 'Passwort-Vergabe für Mitglieder erlaubt' in pg.inner_text('body'))
    pg.screenshot(path='shots/dev4-admin-form.png',full_page=True)
    br.close()
print('\n'.join(res)); print('JS:',[e for e in errs if 'QR scanner' not in e])
