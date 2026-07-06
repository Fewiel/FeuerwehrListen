window.qrScanner = (() => {
    let activeStream = null;
    let scanTimeout = null;
    let detector = null;
    let canvas = null;
    let ctx = null;
    // Generations-Token: jeder start()/stop() erhoeht gen. Asynchrone detect()-Callbacks,
    // die nach einem stop()/erneutem start() zurueckkommen, pruefen ihr myGen gegen gen
    // und feuern dann NICHT mehr ins .NET (verhindert Ghost-Scans nach dem Schliessen).
    let gen = 0;

    // facing: 'user' (Frontkamera, Standard) oder 'environment' (Rueckkamera).
    // facingMode ist stabil und uebersteht Updates - anders als eine deviceId.
    //
    // WICHTIG: Manche (aeltere) Geraete ignorieren facingMode als "ideal" komplett und
    // liefern immer die gleiche (Rueck-)Kamera. Darum wird zuerst {exact: facing}
    // ERZWUNGEN (echtes Umschalten). Nur wenn das Geraet die gewuenschte Kamera nicht hat,
    // wird auf "ideal" und zuletzt auf "irgendeine Kamera" zurueckgefallen.
    async function getCameraStream(facing) {
        var want = facing === 'environment' ? 'environment' : 'user';
        var base = { width: { ideal: 1280 }, height: { ideal: 720 } };

        // 1) Erzwungen (echtes Umschalten front/rueck)
        try {
            return await navigator.mediaDevices.getUserMedia({
                video: Object.assign({}, base, { facingMode: { exact: want } })
            });
        } catch (e) {
            console.warn('Kamera exact "' + want + '" nicht moeglich, Fallback:', e && e.name);
        }
        // 2) Bevorzugt (ideal)
        try {
            return await navigator.mediaDevices.getUserMedia({
                video: Object.assign({}, base, { facingMode: want })
            });
        } catch (e) {
            console.warn('Kamera ideal "' + want + '" nicht moeglich, nehme irgendeine Kamera:', e && e.name);
        }
        // 3) Irgendeine Kamera
        return await navigator.mediaDevices.getUserMedia({ video: true });
    }

    // Pre-process for colored QR codes: boost red channel contrast
    function preprocessImageData(imageData) {
        const data = imageData.data;
        for (let i = 0; i < data.length; i += 4) {
            const r = data[i];
            const b = data[i + 2];
            const val = Math.min(255, Math.max(0, r - b * 0.3));
            data[i] = val;
            data[i + 1] = val;
            data[i + 2] = val;
        }
        return imageData;
    }

    // Feuert ein erkanntes Ergebnis nur, wenn diese Scan-Generation noch aktuell ist
    // (kein stop()/neuer start() dazwischen). invokeMethodAsync ist gegen Fehler
    // (z.B. bereits disposte .NET-Referenz) abgesichert.
    function fireDetected(dotNetRef, myGen, value) {
        if (myGen !== gen || !activeStream) return false;
        console.log('QR detected:', value);
        try {
            var p = dotNetRef.invokeMethodAsync('OnQrCodeDetected', value);
            if (p && typeof p.catch === 'function') p.catch(function () { });
        } catch (e) { }
        return true;
    }

    // Gibt true zurueck, wenn der Scanner tatsaechlich laeuft (Kamera offen + play()),
    // sonst false (kein BarcodeDetector, Video-Element fehlt, Kamera verweigert/Fehler).
    // Das .NET (QrScanner.razor) wertet den Rueckgabewert aus, um "Kein Kamerazugriff"
    // anzuzeigen statt eines schwarzen Rechtecks.
    async function start(videoElementId, dotNetRef, facing) {
        await stop();

        var myGen = ++gen;

        // BarcodeDetector is either native (Android) or polyfilled (Desktop via WASM)
        if (!('BarcodeDetector' in window)) {
            console.error('QR scanner: BarcodeDetector not available');
            return false;
        }

        detector = new BarcodeDetector({ formats: ['qr_code'] });
        console.log('QR scanner: BarcodeDetector ready');

        var videoEl = document.getElementById(videoElementId);
        if (!videoEl) {
            console.warn('QR scanner: video element not found:', videoElementId);
            detector = null;
            return false;
        }

        try {
            activeStream = await getCameraStream(facing || loadFacing());
            // Zwischen await und hier koennte bereits ein stop()/neuer start() gelaufen sein.
            if (myGen !== gen) {
                try { activeStream.getTracks().forEach(function (t) { t.stop(); }); } catch (e) { }
                activeStream = null;
                return false;
            }
            videoEl.srcObject = activeStream;
            await videoEl.play();
            if (myGen !== gen) return false;

            canvas = document.createElement('canvas');
            ctx = canvas.getContext('2d', { willReadFrequently: true });

            var frameCount = 0;
            console.log('QR scanner: scan loop started');

            function scanLoop() {
                if (myGen !== gen || !activeStream || !detector) return;
                var vid = document.getElementById(videoElementId);
                if (!vid || vid.readyState < 2 || !vid.videoWidth) {
                    scanTimeout = setTimeout(scanLoop, 300);
                    return;
                }

                frameCount++;

                // Primary: detect directly from video element (native resolution)
                detector.detect(vid).then(function (barcodes) {
                    if (myGen !== gen) return;
                    if (barcodes.length > 0) {
                        fireDetected(dotNetRef, myGen, barcodes[0].rawValue);
                        scanTimeout = setTimeout(scanLoop, 800);
                        return;
                    }

                    // Secondary: color-preprocessed scan every 3rd frame
                    if (frameCount % 3 === 0) {
                        var vw = vid.videoWidth;
                        var vh = vid.videoHeight;
                        if (canvas.width !== vw || canvas.height !== vh) {
                            canvas.width = vw;
                            canvas.height = vh;
                        }
                        ctx.drawImage(vid, 0, 0, vw, vh);
                        var imageData = ctx.getImageData(0, 0, vw, vh);
                        preprocessImageData(imageData);
                        ctx.putImageData(imageData, 0, 0);

                        detector.detect(canvas).then(function (barcodes2) {
                            if (myGen !== gen) return;
                            if (barcodes2.length > 0) {
                                console.log('QR detected (color-enhanced)');
                                fireDetected(dotNetRef, myGen, barcodes2[0].rawValue);
                                scanTimeout = setTimeout(scanLoop, 800);
                            } else {
                                scanTimeout = setTimeout(scanLoop, 150);
                            }
                        }).catch(function () {
                            if (myGen !== gen) return;
                            scanTimeout = setTimeout(scanLoop, 150);
                        });
                    } else {
                        scanTimeout = setTimeout(scanLoop, 150);
                    }
                }).catch(function () {
                    if (myGen !== gen) return;
                    scanTimeout = setTimeout(scanLoop, 200);
                });
            }

            scanLoop();
            return true;
        } catch (ex) {
            console.error('QR scanner start failed:', ex);
            // Aufraeumen, damit ein spaeterer Start sauber neu beginnt.
            if (activeStream) {
                try { activeStream.getTracks().forEach(function (t) { t.stop(); }); } catch (e) { }
                activeStream = null;
            }
            detector = null;
            canvas = null;
            ctx = null;
            return false;
        }
    }

    async function stop() {
        // Generation erhoehen: laufende detect()-Callbacks feuern danach nicht mehr.
        gen++;
        if (scanTimeout) {
            clearTimeout(scanTimeout);
            scanTimeout = null;
        }
        if (activeStream) {
            activeStream.getTracks().forEach(function (t) { t.stop(); });
            activeStream = null;
        }
        detector = null;
        canvas = null;
        ctx = null;
    }

    async function listCameras() {
        try {
            var tempStream = await navigator.mediaDevices.getUserMedia({ video: true });
            var devices = await navigator.mediaDevices.enumerateDevices();
            tempStream.getTracks().forEach(function (t) { t.stop(); });
            return devices
                .filter(function (d) { return d.kind === 'videoinput'; })
                .map(function (d) { return { deviceId: d.deviceId, label: d.label || d.deviceId }; });
        } catch (ex) {
            console.warn('QR scanner: cannot list cameras:', ex);
            return [];
        }
    }

    async function startPreview(videoElementId, facing) {
        var videoElement = document.getElementById(videoElementId);
        if (!videoElement) return;
        // Vorhandenen Stream zuerst sauber stoppen, damit iOS/Safari die Kamera freigibt
        // und beim Umschalten wirklich die andere Kamera oeffnet (sonst bleibt es auf der alten).
        if (videoElement.srcObject) {
            try { videoElement.srcObject.getTracks().forEach(function (t) { t.stop(); }); } catch (e) { }
            videoElement.srcObject = null;
            await new Promise(function (r) { setTimeout(r, 200); });
        }
        try {
            var stream = await getCameraStream(facing || loadFacing());
            videoElement.srcObject = stream;
            await videoElement.play();
        } catch (ex) {
            console.warn('QR scanner preview failed:', ex);
        }
    }

    function stopPreview(videoElementId) {
        var videoElement = document.getElementById(videoElementId);
        if (videoElement && videoElement.srcObject) {
            videoElement.srcObject.getTracks().forEach(function (t) { t.stop(); });
            videoElement.srcObject = null;
        }
    }

    // Kamera-Ausrichtung geraetebezogen (localStorage) - NICHT nutzerbezogen, uebersteht Updates.
    // Standard ist IMMER 'user' (Frontkamera); nur ein aktives Umstellen speichert 'environment'.
    function saveFacing(facing) {
        try { localStorage.setItem('qr_camera_facing', facing === 'environment' ? 'environment' : 'user'); } catch (e) { }
    }

    function loadFacing() {
        try { return localStorage.getItem('qr_camera_facing') === 'environment' ? 'environment' : 'user'; } catch (e) { return 'user'; }
    }

    return { start: start, stop: stop, listCameras: listCameras, startPreview: startPreview, stopPreview: stopPreview, saveFacing: saveFacing, loadFacing: loadFacing };
})();
