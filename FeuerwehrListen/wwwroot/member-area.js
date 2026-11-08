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

    // Web-Push (nur moderne Browser; alte iPads melden "unsupported" und sehen nichts davon).
    function b64ToBytes(b64) {
        var pad = '='.repeat((4 - b64.length % 4) % 4);
        var raw = atob((b64 + pad).replace(/-/g, '+').replace(/_/g, '/'));
        var out = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }
    function supported() {
        return window.isSecureContext && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    }
    window.fwPush = {
        // "unsupported" | "default" | "granted" | "denied"
        permission: function () { return supported() ? Notification.permission : 'unsupported'; },
        shouldAsk: function () {
            try { return supported() && Notification.permission === 'default' && localStorage.getItem('fw-push-dismissed') !== '1'; }
            catch (e) { return false; }
        },
        dismiss: function () { try { localStorage.setItem('fw-push-dismissed', '1'); } catch (e) { } },
        isSubscribed: function () {
            if (!supported()) return Promise.resolve(false);
            return navigator.serviceWorker.ready.then(function (reg) { return reg.pushManager.getSubscription(); })
                .then(function (sub) { return !!sub; });
        },
        // Fragt die Berechtigung an und liefert {endpoint,p256dh,auth} oder null.
        subscribe: function (publicKey) {
            if (!supported()) return Promise.resolve(null);
            try { localStorage.removeItem('fw-push-dismissed'); } catch (e) { }
            return Notification.requestPermission().then(function (p) {
                if (p !== 'granted') return null;
                return navigator.serviceWorker.ready.then(function (reg) {
                    return reg.pushManager.getSubscription().then(function (sub) {
                        return sub || reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: b64ToBytes(publicKey) });
                    });
                }).then(function (sub) {
                    var j = sub.toJSON();
                    return { endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth };
                });
            });
        },
        unsubscribe: function () {
            if (!supported()) return Promise.resolve(null);
            return navigator.serviceWorker.ready.then(function (reg) { return reg.pushManager.getSubscription(); })
                .then(function (sub) {
                    if (!sub) return null;
                    var ep = sub.endpoint;
                    return sub.unsubscribe().then(function () { return ep; });
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
