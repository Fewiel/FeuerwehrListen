window.qrScanner = (() => {
    let activeStream = null;
    let scanTimeout = null;
    let detector = null;
    let canvas = null;
    let ctx = null;

    function buildConstraints(deviceId) {
        const video = {
            width: { ideal: 1280 },
            height: { ideal: 720 },
            focusMode: { ideal: 'continuous' },
            advanced: [{ focusMode: 'continuous' }]
        };
        if (deviceId) {
            video.deviceId = { exact: deviceId };
        } else {
            video.facingMode = 'environment';
        }
        return { video: video };
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

    async function start(videoElementId, dotNetRef, deviceId) {
        await stop();

        // BarcodeDetector is either native (Android) or polyfilled (Desktop via WASM)
        if (!('BarcodeDetector' in window)) {
            console.error('QR scanner: BarcodeDetector not available');
            return;
        }

        detector = new BarcodeDetector({ formats: ['qr_code'] });
        console.log('QR scanner: BarcodeDetector ready');

        const videoEl = document.getElementById(videoElementId);
        if (!videoEl) {
            console.warn('QR scanner: video element not found:', videoElementId);
            return;
        }

        try {
            const constraints = buildConstraints(deviceId);
            activeStream = await navigator.mediaDevices.getUserMedia(constraints);
            videoEl.srcObject = activeStream;
            await videoEl.play();

            canvas = document.createElement('canvas');
            ctx = canvas.getContext('2d', { willReadFrequently: true });

            let frameCount = 0;
            console.log('QR scanner: scan loop started');

            function scanLoop() {
                if (!activeStream || !detector) return;
                const vid = document.getElementById(videoElementId);
                if (!vid || vid.readyState < 2 || !vid.videoWidth) {
                    scanTimeout = setTimeout(scanLoop, 300);
                    return;
                }

                frameCount++;

                // Primary: detect directly from video element (native resolution)
                detector.detect(vid).then(function (barcodes) {
                    if (barcodes.length > 0) {
                        console.log('QR detected:', barcodes[0].rawValue);
                        dotNetRef.invokeMethodAsync('OnQrCodeDetected', barcodes[0].rawValue);
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
                            if (barcodes2.length > 0) {
                                console.log('QR detected (color-enhanced):', barcodes2[0].rawValue);
                                dotNetRef.invokeMethodAsync('OnQrCodeDetected', barcodes2[0].rawValue);
                                scanTimeout = setTimeout(scanLoop, 800);
                            } else {
                                scanTimeout = setTimeout(scanLoop, 150);
                            }
                        }).catch(function () {
                            scanTimeout = setTimeout(scanLoop, 150);
                        });
                    } else {
                        scanTimeout = setTimeout(scanLoop, 150);
                    }
                }).catch(function () {
                    scanTimeout = setTimeout(scanLoop, 200);
                });
            }

            scanLoop();
        } catch (ex) {
            console.error('QR scanner start failed:', ex);
        }
    }

    async function stop() {
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

    async function startPreview(videoElementId, deviceId) {
        var videoElement = document.getElementById(videoElementId);
        if (!videoElement) return;
        try {
            var constraints = buildConstraints(deviceId);
            var stream = await navigator.mediaDevices.getUserMedia(constraints);
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

    function saveDeviceId(deviceId) {
        localStorage.setItem('qr_camera_deviceId', deviceId);
    }

    function loadDeviceId() {
        return localStorage.getItem('qr_camera_deviceId');
    }

    return { start: start, stop: stop, listCameras: listCameras, startPreview: startPreview, stopPreview: stopPreview, saveDeviceId: saveDeviceId, loadDeviceId: loadDeviceId };
})();
