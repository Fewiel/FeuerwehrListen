// Mitgliederbereich: Lichtbild zuschneiden (4:5, max. 600x750, JPEG) und Bildschirm wach halten.
// Bewusst ES5-nah (alte iPads), keine Module.
(function () {
    function loadImage(file) {
        return new Promise(function (resolve, reject) {
            var url = URL.createObjectURL(file);
            var img = new Image();
            img.onload = function () { resolve(img); URL.revokeObjectURL(url); };
            img.onerror = function () { reject(new Error("Bild konnte nicht gelesen werden.")); URL.revokeObjectURL(url); };
            img.src = url;
        });
    }

    window.fwPhoto = {
        // Liest die Datei aus dem <input type=file id=inputId>, schneidet mittig auf 4:5 zu
        // und liefert eine data:image/jpeg-URL (oder null, wenn keine Datei gewaehlt ist).
        fromInput: function (inputId) {
            var input = document.getElementById(inputId);
            if (!input || !input.files || input.files.length === 0) return Promise.resolve(null);
            return loadImage(input.files[0]).then(function (img) {
                var targetRatio = 4 / 5;
                var sw = img.naturalWidth, sh = img.naturalHeight;
                var cropW = sw, cropH = sh;
                if (sw / sh > targetRatio) cropW = sh * targetRatio; else cropH = sw / targetRatio;
                var sx = (sw - cropW) / 2, sy = (sh - cropH) / 2;
                var outW = Math.min(600, Math.round(cropW)), outH = Math.round(outW / targetRatio);
                var canvas = document.createElement("canvas");
                canvas.width = outW; canvas.height = outH;
                var ctx = canvas.getContext("2d");
                ctx.fillStyle = "#ffffff";
                ctx.fillRect(0, 0, outW, outH);
                ctx.drawImage(img, sx, sy, cropW, cropH, 0, 0, outW, outH);
                input.value = "";
                return canvas.toDataURL("image/jpeg", 0.85);
            });
        }
    };

    var lock = null;
    window.fwWakeLock = {
        request: function () {
            try {
                if (navigator.wakeLock && navigator.wakeLock.request)
                    navigator.wakeLock.request("screen").then(function (l) { lock = l; }).catch(function () { });
            } catch (e) { }
        },
        release: function () {
            try { if (lock) { lock.release(); lock = null; } } catch (e) { }
        }
    };
})();
