// Einfaches Unterschriften-Pad auf <canvas>.
// Nutzt Maus- UND Touch-Events, damit es auch auf aelteren Tablets (iOS < 13,
// die keine Pointer Events haben) funktioniert. preventDefault (nicht-passiv)
// verhindert das Scrollen der Seite beim Zeichnen.
// Merkt sich pro Canvas-Id die registrierten Listener, damit detach() sie sauber
// entfernen kann. Sonst bleibt v.a. der auf window haengende "mouseup"-Listener
// haengen -> Leak im langlebigen Server-Circuit (Blazor Server / iOS-12-Geraete).
window.__sigPads = window.__sigPads || {};

window.signaturePad = {
    attach: function (id) {
        var c = document.getElementById(id);
        if (!c || c.dataset.attached) return;
        c.dataset.attached = "1";

        // Verhindert Browser-Gesten (Scroll/Zoom) auf dem Canvas (iOS 13+).
        c.style.touchAction = "none";
        c.style.msTouchAction = "none";

        // Fuer scharfe Unterschriften auf Retina/HiDPI: Backing-Store in Geraete-Pixeln,
        // Zeichenkoordinaten aber weiter in CSS-Pixeln (ctx.scale). Das Canvas hat feste
        // width/height-Attribute (640x170) und wird per CSS auf 100%/150px gestreckt -
        // die pos()-Umrechnung nutzt weiter c.width/r.width, bleibt also korrekt.
        var dpr = window.devicePixelRatio || 1;
        var cssW = c.width, cssH = c.height;
        if (dpr > 1) {
            c.width = Math.round(cssW * dpr);
            c.height = Math.round(cssH * dpr);
        }

        var ctx = c.getContext("2d");
        if (dpr > 1) ctx.scale(dpr, dpr);
        ctx.lineWidth = 2.2;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.strokeStyle = "#171B21";
        var drawing = false, last = null;

        function pos(e) {
            var r = c.getBoundingClientRect();
            var t = (e.touches && e.touches[0]) ? e.touches[0] : e;
            // Zeichnen erfolgt im (per ctx.scale) skalierten CSS-Koordinatensystem,
            // daher auf cssW/cssH statt c.width/c.height beziehen.
            return {
                x: (t.clientX - r.left) * (cssW / r.width),
                y: (t.clientY - r.top) * (cssH / r.height)
            };
        }
        function down(e) {
            drawing = true;
            last = pos(e);
            if (e.cancelable) e.preventDefault();
        }
        function move(e) {
            if (!drawing) return;
            var p = pos(e);
            ctx.beginPath();
            ctx.moveTo(last.x, last.y);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
            last = p;
            c.dataset.dirty = "1";
            if (e.cancelable) e.preventDefault();
        }
        function up() { drawing = false; }

        // Touch-Optionen (passive:false, damit preventDefault das Scrollen stoppt).
        // Fallback fuer sehr alte Browser ohne Options-Objekt.
        var opts = false;
        try {
            var probe = Object.defineProperty({}, "passive", { get: function () { opts = { passive: false }; } });
            window.addEventListener("x-probe", null, probe);
            window.removeEventListener("x-probe", null, probe);
        } catch (e) { }

        // Maus (Desktop)
        c.addEventListener("mousedown", down);
        c.addEventListener("mousemove", move);
        window.addEventListener("mouseup", up);

        // Touch (Tablets, inkl. alte iPads).
        c.addEventListener("touchstart", down, opts);
        c.addEventListener("touchmove", move, opts);
        c.addEventListener("touchend", up);
        c.addEventListener("touchcancel", up);

        // Listener merken, damit detach() sie exakt wieder entfernen kann.
        window.__sigPads[id] = { c: c, down: down, move: move, up: up, opts: opts };
    },
    // Entfernt alle Listener (v.a. den window-"mouseup") und raeumt die Registrierung auf.
    // Wird aus SignaturePad.razor DisposeAsync aufgerufen.
    detach: function (id) {
        var s = window.__sigPads[id];
        if (!s) return;
        var c = s.c;
        try {
            if (c) {
                c.removeEventListener("mousedown", s.down);
                c.removeEventListener("mousemove", s.move);
                c.removeEventListener("touchstart", s.down, s.opts);
                c.removeEventListener("touchmove", s.move, s.opts);
                c.removeEventListener("touchend", s.up);
                c.removeEventListener("touchcancel", s.up);
                c.dataset.attached = "";
            }
            window.removeEventListener("mouseup", s.up);
        } catch (e) { }
        delete window.__sigPads[id];
    },
    clear: function (id) {
        var c = document.getElementById(id);
        if (!c) return;
        c.getContext("2d").clearRect(0, 0, c.width, c.height);
        c.dataset.dirty = "";
        c.dataset.loaded = "";
    },
    load: function (id, dataUrl) {
        var c = document.getElementById(id);
        if (!c || !dataUrl) return;
        var img = new Image();
        img.onload = function () {
            c.getContext("2d").drawImage(img, 0, 0, c.width, c.height);
        };
        img.src = dataUrl;
        c.dataset.loaded = "1";
    },
    // Liefert PNG-Data-URL oder "" wenn leer/unverändert-leer
    toDataUrl: function (id) {
        var c = document.getElementById(id);
        if (!c) return "";
        if (!c.dataset.dirty && !c.dataset.loaded) return "";
        return c.toDataURL("image/png");
    }
};
