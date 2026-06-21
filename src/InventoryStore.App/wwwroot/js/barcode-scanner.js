// Unified barcode scanner used by the in-app modal, the SKU edit field, and the
// Terminal. It hides the decode engine behind a tiny API so every call site reads
// the same way:
//
//   var ctl = BarcodeScanner.scanFromVideo(videoEl, {
//       onResult: function (text) { ... },   // fires once, then the camera stops
//       onError:  function (err)  { ... }
//   });
//   ctl.stop();                               // stop the camera + decode loop
//
//   BarcodeScanner.scanFromImageFile(file)    // -> Promise<text> (photo capture)
//
// Engine order: the browser's native BarcodeDetector first (fast, hardware backed,
// available on Android Chrome / Edge), then a zbar-wasm fallback for everything
// else (iOS Safari, desktop Firefox). Both beat the old ZXing-js port at reading
// 1D retail barcodes from a live camera.
(function () {
    'use strict';

    // Retail/inventory formats we care about. Names match the BarcodeDetector spec.
    var FORMATS = ['upc_a', 'upc_e', 'ean_13', 'ean_8', 'code_128', 'qr_code'];
    var ZBAR_URL = 'https://cdn.jsdelivr.net/npm/@undecaf/zbar-wasm@0.11.0/dist/index.js';

    // ── Native BarcodeDetector ─────────────────────────────────────────────
    // Resolves to a detector instance, or null if the platform can't do 1D codes.
    var _nativePromise = null;
    function getNativeDetector() {
        if (_nativePromise) return _nativePromise;
        _nativePromise = (function () {
            if (!('BarcodeDetector' in window)) return Promise.resolve(null);
            return window.BarcodeDetector.getSupportedFormats()
                .then(function (supported) {
                    var fmts = FORMATS.filter(function (f) { return supported.indexOf(f) !== -1; });
                    // A platform that only supports QR is useless for our barcodes; fall back.
                    if (fmts.indexOf('ean_13') === -1 && fmts.indexOf('code_128') === -1) return null;
                    return new window.BarcodeDetector({ formats: fmts });
                })
                .catch(function () { return null; });
        })();
        return _nativePromise;
    }

    // ── zbar-wasm fallback ─────────────────────────────────────────────────
    // Loaded on demand so the script (and its .wasm) only download when needed.
    var _zbarPromise = null;
    function getZbar() {
        if (_zbarPromise) return _zbarPromise;
        _zbarPromise = new Promise(function (resolve, reject) {
            if (window.zbarWasm) { resolve(window.zbarWasm); return; }
            var s = document.createElement('script');
            s.src = ZBAR_URL;
            s.async = true;
            s.onload = function () {
                window.zbarWasm ? resolve(window.zbarWasm) : reject(new Error('Barcode engine unavailable.'));
            };
            s.onerror = function () { reject(new Error('Could not load the barcode engine.')); };
            document.head.appendChild(s);
        });
        return _zbarPromise;
    }

    // Draw a frame onto a reusable canvas and pull its pixels for zbar. The longest
    // edge is capped so big camera/photo frames stay fast to decode.
    var _canvas = null, _ctx = null;
    function toImageData(source, sw, sh, maxEdge) {
        if (!_canvas) {
            _canvas = document.createElement('canvas');
            _ctx = _canvas.getContext('2d', { willReadFrequently: true });
        }
        var scale = Math.min(1, maxEdge / Math.max(sw, sh));
        var w = Math.max(1, Math.round(sw * scale));
        var h = Math.max(1, Math.round(sh * scale));
        if (_canvas.width !== w) _canvas.width = w;
        if (_canvas.height !== h) _canvas.height = h;
        _ctx.drawImage(source, 0, 0, w, h);
        return _ctx.getImageData(0, 0, w, h);
    }

    // ── Live video scanning ────────────────────────────────────────────────
    function scanFromVideo(videoEl, opts) {
        opts = opts || {};
        var onResult = opts.onResult || function () {};
        var onError  = opts.onError  || function () {};
        var stopped  = false;
        var rafId    = null;
        var stream   = null;
        var detector = null;   // native detector, when available
        var zbar     = null;   // zbar module, when falling back

        function cleanup() {
            stopped = true;
            if (rafId) { cancelAnimationFrame(rafId); rafId = null; }
            if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; }
            if (videoEl) videoEl.srcObject = null;
        }

        function found(text) {
            if (stopped || !text) return;
            cleanup();
            onResult(text);
        }

        function loop() {
            if (stopped) return;
            // Wait until the stream has real frames to read.
            if (videoEl.readyState < 2 || !videoEl.videoWidth) { rafId = requestAnimationFrame(loop); return; }
            var work;
            if (detector) {
                work = detector.detect(videoEl).then(function (codes) {
                    if (codes && codes.length) found(codes[0].rawValue);
                });
            } else {
                var img = toImageData(videoEl, videoEl.videoWidth, videoEl.videoHeight, 640);
                work = zbar.scanImageData(img).then(function (syms) {
                    if (syms && syms.length) found(syms[0].decode());
                });
            }
            // Errors mid-stream (a blurry frame, a busy decoder) are non-fatal; keep going.
            work.catch(function () {}).then(function () {
                if (!stopped) rafId = requestAnimationFrame(loop);
            });
        }

        navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } } })
            .then(function (s) {
                if (stopped) { s.getTracks().forEach(function (t) { t.stop(); }); return; }
                stream = s;
                videoEl.srcObject = s;
                videoEl.setAttribute('playsinline', 'true');
                return videoEl.play().catch(function () {});
            })
            .then(function () {
                if (stopped) return;
                return getNativeDetector();
            })
            .then(function (nat) {
                if (stopped) return;
                if (nat) { detector = nat; loop(); return; }
                return getZbar().then(function (z) { if (!stopped) { zbar = z; loop(); } });
            })
            .catch(function (err) { cleanup(); onError(err); });

        return { stop: cleanup };
    }

    // ── Still image (photo / file upload) scanning ─────────────────────────
    function scanFromImageFile(file) {
        var url = URL.createObjectURL(file);
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.onload = function () { resolve(img); };
            img.onerror = function () { reject(new Error('Could not load image.')); };
            img.src = url;
        })
        .then(function (image) {
            return getNativeDetector().then(function (nat) {
                if (nat) {
                    return nat.detect(image).then(function (codes) {
                        if (codes && codes.length) return codes[0].rawValue;
                        throw new Error('No barcode found.');
                    });
                }
                return getZbar().then(function (z) {
                    // Stills get a larger cap than live frames: a single decode can afford the detail.
                    var data = toImageData(image, image.naturalWidth, image.naturalHeight, 1600);
                    return z.scanImageData(data).then(function (syms) {
                        if (syms && syms.length) return syms[0].decode();
                        throw new Error('No barcode found.');
                    });
                });
            });
        })
        .finally(function () { URL.revokeObjectURL(url); });
    }

    window.BarcodeScanner = {
        scanFromVideo: scanFromVideo,
        scanFromImageFile: scanFromImageFile
    };
})();
