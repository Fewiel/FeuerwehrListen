#!/usr/bin/env python3
# QA Server-Modus (alte iPads, iOS-12-UA): Kiosk ueber Whitelist, Login von aussen, Mitgliederbereich, Fuehrungskreis.
from playwright.sync_api import sync_playwright
LAN='http://192.168.2.81:5180'; LOCAL='http://127.0.0.1:5180'; PW='Feuerwehr2026!'
UA='Mozilla/5.0 (iPad; CPU OS 12_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/12.1.2 Mobile/15E148 Safari/604.1'
ipad={'user_agent':UA,'viewport':{'width':1024,'height':768},'device_scale_factor':1}
with sync_playwright() as p:
    br=p.chromium.launch(); errs=[]
    def ctx(base,user=None):
        c=br.new_context(**ipad)
        if user:
            j=c.request.post(base+'/client-api/auth/login',data={'username':user,'password':PW}).json()
            c.request.get(base+'/client-api/auth/complete?ticket='+j['ticket'],max_redirects=0)
        return c
    def go(c,url,name):
        pg=c.new_page(); pg.on('pageerror',lambda e: errs.append(f'{name}: {e}'))
        pg.goto(url,wait_until='load'); pg.wait_for_timeout(6000)
        pg.screenshot(path=f'shots/{name}.png',full_page=True)
        server = pg.locator('#fw-heartbeat').count()>0
        return pg, server
    pg,srv=go(ctx(LOCAL),LOCAL+'/','ipad-kiosk'); t=pg.inner_text('body')
    print('Kiosk iPad-UA von Whitelist: Server-Modus=',srv,'| Listen sichtbar=', 'Martinsumzug' in t, '| Login-Button=', 'Login' in t)
    pg,srv=go(ctx(LAN),LAN+'/firesafetywatches','ipad-anon-aussen'); print('iPad-UA anonym von aussen -> ', pg.url.replace(LAN,''), '| Server-Modus=',srv)
    m=ctx(LAN,'1234'); pg,srv=go(m,LAN+'/mein','ipad-mein'); t=pg.inner_text('body'); print('iPad-UA Mitglied /mein: Server-Modus=',srv,'| Inhalt ok=', 'Hallo, Max' in t and 'Martinsumzug' in t)
    pg,srv=go(m,LAN+'/mein/ausweis','ipad-ausweis'); t=pg.inner_text('body'); print('iPad-UA Ausweis: Server-Modus=',srv,'| Geb./Aussteller=', '17.05.1995' in t and 'Bürgermeister' in t)
    pg.get_by_role('button',name='Vollbild zum Vorzeigen').click(); pg.wait_for_timeout(1500); pg.screenshot(path='shots/ipad-ausweis-vollbild.png'); print('Vollbild im Server-Modus klickbar:', pg.locator('.fwc-full').count()==1)
    pg,srv=go(ctx(LAN,'1001'),LAN+'/fuehrungskreis/aufgaben','ipad-aufgaben'); print('iPad-UA Fuehrungskreis Aufgaben: Server-Modus=',srv,'| Inhalt ok=','Dienstplan 2027' in pg.inner_text('body'))
    print('JS-Fehler:',errs); br.close()
