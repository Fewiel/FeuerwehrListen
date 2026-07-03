// Einfaches Unterschriften-Pad auf <canvas>
window.signaturePad = {
    attach: function (id) {
        var c = document.getElementById(id);
        if (!c || c.dataset.attached) return;
        c.dataset.attached = "1";
        var ctx = c.getContext("2d");
        ctx.lineWidth = 2.2;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.strokeStyle = "#171B21";
        var drawing = false, last = null;

        function pos(e) {
            var r = c.getBoundingClientRect();
            var t = (e.touches && e.touches[0]) ? e.touches[0] : e;
            return {
                x: (t.clientX - r.left) * (c.width / r.width),
                y: (t.clientY - r.top) * (c.height / r.height)
            };
        }
        function down(e) { drawing = true; last = pos(e); e.preventDefault(); }
        function move(e) {
            if (!drawing) return;
            var p = pos(e);
            ctx.beginPath();
            ctx.moveTo(last.x, last.y);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
            last = p;
            c.dataset.dirty = "1";
            e.preventDefault();
        }
        function up() { drawing = false; }

        c.addEventListener("pointerdown", down);
        c.addEventListener("pointermove", move);
        window.addEventListener("pointerup", up);
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
