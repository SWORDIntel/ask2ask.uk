// Comprehensive Browser Fingerprinting and Tracking System
// Educational/Research Data Collection Experiment

(function() {
    'use strict';

    const collectedData = {
        timestamp: new Date().toISOString(),
        sessionId: generateSessionId(),
        basicInfo: {},
        fingerprints: {},
        hardware: {},
        network: {},
        behavioral: {
            mouseMovements: [],
            clicks: [],
            scrollEvents: [],
            keystrokes: [],
            timings: {}
        },
        performance: {},
        permissions: {},
        storage: {}
    };

    // Generate unique session ID
    function generateSessionId() {
        return 'sess_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }

    // ====================
    // BASIC BROWSER INFO
    // ====================
    function collectBasicInfo() {
        collectedData.basicInfo = {
            userAgent: navigator.userAgent,
            platform: navigator.platform,
            language: navigator.language,
            languages: navigator.languages,
            cookieEnabled: navigator.cookieEnabled,
            doNotTrack: navigator.doNotTrack,
            hardwareConcurrency: navigator.hardwareConcurrency,
            maxTouchPoints: navigator.maxTouchPoints,
            vendor: navigator.vendor,
            vendorSub: navigator.vendorSub,
            product: navigator.product,
            productSub: navigator.productSub,
            appName: navigator.appName,
            appCodeName: navigator.appCodeName,
            appVersion: navigator.appVersion,
            oscpu: navigator.oscpu,
            buildID: navigator.buildID,
            onLine: navigator.onLine
        };
    }

    // ====================
    // SCREEN & DISPLAY
    // ====================
    function collectScreenInfo() {
        collectedData.hardware.screen = {
            width: screen.width,
            height: screen.height,
            availWidth: screen.availWidth,
            availHeight: screen.availHeight,
            colorDepth: screen.colorDepth,
            pixelDepth: screen.pixelDepth,
            orientation: screen.orientation ? screen.orientation.type : 'unknown',
            innerWidth: window.innerWidth,
            innerHeight: window.innerHeight,
            outerWidth: window.outerWidth,
            outerHeight: window.outerHeight,
            devicePixelRatio: window.devicePixelRatio,
            screenX: window.screenX,
            screenY: window.screenY
        };
    }

    // ====================
    // TIMEZONE & LOCALE
    // ====================
    function collectTimezoneInfo() {
        const date = new Date();
        collectedData.basicInfo.timezone = {
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
            timezoneOffset: date.getTimezoneOffset(),
            locale: Intl.DateTimeFormat().resolvedOptions().locale,
            calendar: Intl.DateTimeFormat().resolvedOptions().calendar,
            numberingSystem: Intl.DateTimeFormat().resolvedOptions().numberingSystem,
            localTime: date.toLocaleString(),
            utcTime: date.toUTCString()
        };
    }

    // ====================
    // CANVAS FINGERPRINTING
    // ====================
    function getCanvasFingerprint() {
        try {
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            const text = 'ASK2ASK.UK Fingerprint 🔍 <>@#$%^&*()';

            canvas.width = 280;
            canvas.height = 60;

            ctx.textBaseline = 'top';
            ctx.font = '14px "Arial"';
            ctx.textBaseline = 'alphabetic';
            ctx.fillStyle = '#f60';
            ctx.fillRect(125, 1, 62, 20);
            ctx.fillStyle = '#069';
            ctx.fillText(text, 2, 15);
            ctx.fillStyle = 'rgba(102, 204, 0, 0.7)';
            ctx.fillText(text, 4, 17);

            const dataURL = canvas.toDataURL();
            const hash = simpleHash(dataURL);

            collectedData.fingerprints.canvas = {
                hash: hash,
                dataURL: dataURL.substring(0, 100) + '...'
            };
        } catch (e) {
            collectedData.fingerprints.canvas = { error: e.message };
        }
    }

    // ====================
    // WEBGL FINGERPRINTING
    // ====================
    function getWebGLFingerprint() {
        try {
            const canvas = document.createElement('canvas');
            const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');

            if (!gl) {
                collectedData.fingerprints.webgl = { error: 'WebGL not supported' };
                return;
            }

            const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');

            collectedData.fingerprints.webgl = {
                vendor: gl.getParameter(gl.VENDOR),
                renderer: gl.getParameter(gl.RENDERER),
                version: gl.getParameter(gl.VERSION),
                shadingLanguageVersion: gl.getParameter(gl.SHADING_LANGUAGE_VERSION),
                unmaskedVendor: debugInfo ? gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL) : 'N/A',
                unmaskedRenderer: debugInfo ? gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) : 'N/A',
                maxTextureSize: gl.getParameter(gl.MAX_TEXTURE_SIZE),
                maxVertexAttribs: gl.getParameter(gl.MAX_VERTEX_ATTRIBS),
                maxViewportDims: gl.getParameter(gl.MAX_VIEWPORT_DIMS),
                supportedExtensions: gl.getSupportedExtensions()
            };
        } catch (e) {
            collectedData.fingerprints.webgl = { error: e.message };
        }
    }

    // ====================
    // AUDIO FINGERPRINTING
    // ====================
    function getAudioFingerprint() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) {
                collectedData.fingerprints.audio = { error: 'AudioContext not supported' };
                return;
            }

            const context = new AudioContext();
            const oscillator = context.createOscillator();
            const analyser = context.createAnalyser();
            const gainNode = context.createGain();
            const scriptProcessor = context.createScriptProcessor(4096, 1, 1);

            gainNode.gain.value = 0;
            oscillator.type = 'triangle';
            oscillator.connect(analyser);
            analyser.connect(scriptProcessor);
            scriptProcessor.connect(gainNode);
            gainNode.connect(context.destination);

            scriptProcessor.onaudioprocess = function(event) {
                const output = event.outputBuffer.getChannelData(0);
                const hash = simpleHash(output.slice(0, 30).join(','));

                collectedData.fingerprints.audio = {
                    hash: hash,
                    sampleRate: context.sampleRate,
                    state: context.state,
                    maxChannelCount: context.destination.maxChannelCount,
                    numberOfInputs: scriptProcessor.numberOfInputs,
                    numberOfOutputs: scriptProcessor.numberOfOutputs,
                    channelCount: scriptProcessor.channelCount
                };

                oscillator.stop();
                scriptProcessor.disconnect();
            };

            oscillator.start(0);
        } catch (e) {
            collectedData.fingerprints.audio = { error: e.message };
        }
    }

    // ====================
    // FONT DETECTION
    // ====================
    function detectFonts() {
        const baseFonts = ['monospace', 'sans-serif', 'serif'];
        const testFonts = [
            'Arial', 'Verdana', 'Times New Roman', 'Courier New', 'Georgia', 'Palatino',
            'Garamond', 'Bookman', 'Comic Sans MS', 'Trebuchet MS', 'Impact', 'Lucida Console',
            'Tahoma', 'Helvetica', 'Calibri', 'Cambria', 'Consolas', 'Monaco', 'Roboto',
            'Ubuntu', 'Segoe UI', 'SF Pro Display', 'Apple Color Emoji', 'Noto Color Emoji'
        ];

        const testString = 'mmmmmmmmmmlli';
        const testSize = '72px';
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        const baseFontWidths = {};
        baseFonts.forEach(baseFont => {
            ctx.font = testSize + ' ' + baseFont;
            baseFontWidths[baseFont] = ctx.measureText(testString).width;
        });

        const detectedFonts = [];
        testFonts.forEach(font => {
            let detected = false;
            baseFonts.forEach(baseFont => {
                ctx.font = testSize + ' ' + font + ', ' + baseFont;
                const width = ctx.measureText(testString).width;
                if (width !== baseFontWidths[baseFont]) {
                    detected = true;
                }
            });
            if (detected) {
                detectedFonts.push(font);
            }
        });

        collectedData.fingerprints.fonts = detectedFonts;
    }

    // ====================
    // PLUGINS DETECTION
    // ====================
    function detectPlugins() {
        const plugins = [];
        for (let i = 0; i < navigator.plugins.length; i++) {
            const plugin = navigator.plugins[i];
            plugins.push({
                name: plugin.name,
                filename: plugin.filename,
                description: plugin.description,
                version: plugin.version
            });
        }
        collectedData.hardware.plugins = plugins;
    }

    // ====================
    // MEDIA DEVICES
    // ====================
    async function detectMediaDevices() {
        try {
            if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
                const devices = await navigator.mediaDevices.enumerateDevices();
                collectedData.hardware.mediaDevices = devices.map(device => ({
                    kind: device.kind,
                    label: device.label,
                    deviceId: device.deviceId ? 'present' : 'none',
                    groupId: device.groupId ? 'present' : 'none'
                }));
            }
        } catch (e) {
            collectedData.hardware.mediaDevices = { error: e.message };
        }
    }

    // ====================
    // BATTERY STATUS
    // ====================
    async function getBatteryInfo() {
        try {
            if (navigator.getBattery) {
                const battery = await navigator.getBattery();
                collectedData.hardware.battery = {
                    charging: battery.charging,
                    chargingTime: battery.chargingTime,
                    dischargingTime: battery.dischargingTime,
                    level: battery.level
                };
            }
        } catch (e) {
            collectedData.hardware.battery = { error: e.message };
        }
    }

    // ====================
    // NETWORK INFORMATION
    // ====================
    function getNetworkInfo() {
        const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        if (connection) {
            collectedData.network = {
                effectiveType: connection.effectiveType,
                downlink: connection.downlink,
                rtt: connection.rtt,
                saveData: connection.saveData,
                type: connection.type
            };
        }
    }

    // ====================
    // GEOLOCATION
    // ====================
    function getGeolocation() {
        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(
                (position) => {
                    collectedData.network.geolocation = {
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracy: position.coords.accuracy,
                        altitude: position.coords.altitude,
                        altitudeAccuracy: position.coords.altitudeAccuracy,
                        heading: position.coords.heading,
                        speed: position.coords.speed,
                        timestamp: position.timestamp
                    };
                    updateDisplay();
                    sendDataToServer();
                },
                (error) => {
                    collectedData.network.geolocation = { error: error.message, code: error.code };
                    updateDisplay();
                }
            );
        }
    }

    // ====================
    // STORAGE & CACHING
    // ====================
    function checkStorage() {
        collectedData.storage = {
            localStorage: typeof(Storage) !== 'undefined' && !!window.localStorage,
            sessionStorage: typeof(Storage) !== 'undefined' && !!window.sessionStorage,
            indexedDB: !!window.indexedDB,
            cookieEnabled: navigator.cookieEnabled
        };

        // Try to estimate storage quota
        if (navigator.storage && navigator.storage.estimate) {
            navigator.storage.estimate().then(estimate => {
                collectedData.storage.quota = estimate.quota;
                collectedData.storage.usage = estimate.usage;
                updateDisplay();
            });
        }
    }

    // ====================
    // PERFORMANCE METRICS
    // ====================
    function getPerformanceMetrics() {
        if (window.performance) {
            const perfData = window.performance.getEntriesByType('navigation')[0];
            collectedData.performance = {
                memory: performance.memory ? {
                    jsHeapSizeLimit: performance.memory.jsHeapSizeLimit,
                    totalJSHeapSize: performance.memory.totalJSHeapSize,
                    usedJSHeapSize: performance.memory.usedJSHeapSize
                } : 'not available',
                timing: perfData ? {
                    domainLookup: perfData.domainLookupEnd - perfData.domainLookupStart,
                    tcpConnect: perfData.connectEnd - perfData.connectStart,
                    request: perfData.responseStart - perfData.requestStart,
                    response: perfData.responseEnd - perfData.responseStart,
                    domProcessing: perfData.domComplete - perfData.domLoading,
                    loadComplete: perfData.loadEventEnd - perfData.loadEventStart
                } : 'not available',
                timeOrigin: performance.timeOrigin,
                now: performance.now()
            };
        }
    }

    // ====================
    // BEHAVIORAL TRACKING
    // ====================
    function trackBehavior() {
        // Mouse movements (sample every 100ms to avoid overwhelming data)
        let lastMouseEvent = 0;
        document.addEventListener('mousemove', (e) => {
            const now = Date.now();
            if (now - lastMouseEvent > 100) {
                collectedData.behavioral.mouseMovements.push({
                    x: e.clientX,
                    y: e.clientY,
                    timestamp: now
                });
                lastMouseEvent = now;

                // Keep only last 100 movements
                if (collectedData.behavioral.mouseMovements.length > 100) {
                    collectedData.behavioral.mouseMovements.shift();
                }
            }
        });

        // Clicks
        document.addEventListener('click', (e) => {
            collectedData.behavioral.clicks.push({
                x: e.clientX,
                y: e.clientY,
                target: e.target.tagName,
                timestamp: Date.now()
            });
        });

        // Scroll events
        let lastScroll = 0;
        window.addEventListener('scroll', (e) => {
            const now = Date.now();
            if (now - lastScroll > 200) {
                collectedData.behavioral.scrollEvents.push({
                    x: window.scrollX,
                    y: window.scrollY,
                    timestamp: now
                });
                lastScroll = now;
            }
        });

        // Keystroke timing (not content, just timing patterns)
        let lastKeyTime = 0;
        document.addEventListener('keydown', (e) => {
            const now = Date.now();
            const timeSinceLast = lastKeyTime ? now - lastKeyTime : 0;
            collectedData.behavioral.keystrokes.push({
                timeSinceLast: timeSinceLast,
                timestamp: now,
                key: e.key.length === 1 ? 'character' : 'special'
            });
            lastKeyTime = now;

            // Keep only last 50 keystrokes
            if (collectedData.behavioral.keystrokes.length > 50) {
                collectedData.behavioral.keystrokes.shift();
            }
        });

        // Page visibility
        document.addEventListener('visibilitychange', () => {
            collectedData.behavioral.timings.visibilityChange = {
                hidden: document.hidden,
                timestamp: Date.now()
            };
        });

        // Page focus/blur
        window.addEventListener('focus', () => {
            collectedData.behavioral.timings.focus = Date.now();
        });

        window.addEventListener('blur', () => {
            collectedData.behavioral.timings.blur = Date.now();
        });
    }

    // ====================
    // ADVANCED FEATURES
    // ====================
    function checkAdvancedFeatures() {
        collectedData.hardware.features = {
            webGL: !!window.WebGLRenderingContext,
            webGL2: !!window.WebGL2RenderingContext,
            webRTC: !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia),
            webWorkers: typeof(Worker) !== 'undefined',
            serviceWorkers: 'serviceWorker' in navigator,
            webAssembly: typeof WebAssembly === 'object',
            notifications: 'Notification' in window,
            pushNotifications: 'PushManager' in window,
            vibration: 'vibrate' in navigator,
            webSockets: 'WebSocket' in window,
            webAudio: !!(window.AudioContext || window.webkitAudioContext),
            speech: 'speechSynthesis' in window,
            bluetooth: 'bluetooth' in navigator,
            usb: 'usb' in navigator,
            gamepad: 'getGamepads' in navigator,
            virtualReality: 'getVRDisplays' in navigator || 'xr' in navigator,
            touchEvents: 'ontouchstart' in window,
            pointerEvents: 'onpointerdown' in window,
            deviceOrientation: 'DeviceOrientationEvent' in window,
            deviceMotion: 'DeviceMotionEvent' in window,
            ambientLight: 'AmbientLightSensor' in window,
            proximity: 'ProximitySensor' in window
        };
    }

    // ====================
    // HTTP HEADERS (via fetch timing)
    // ====================
    async function detectHTTPInfo() {
        try {
            const response = await fetch(window.location.href, { method: 'HEAD' });
            collectedData.network.http = {
                status: response.status,
                statusText: response.statusText,
                redirected: response.redirected,
                type: response.type,
                url: response.url
            };
        } catch (e) {
            collectedData.network.http = { error: e.message };
        }
    }

    // ====================
    // UTILITY FUNCTIONS
    // ====================
    function simpleHash(str) {
        let hash = 0;
        for (let i = 0; i < str.length; i++) {
            const char = str.charCodeAt(i);
            hash = ((hash << 5) - hash) + char;
            hash = hash & hash;
        }
        return hash.toString(36);
    }

    // ====================
    // UPDATE DISPLAY
    // ====================
    function updateDisplay() {
        const outputElement = document.getElementById('collected-data');
        if (outputElement) {
            outputElement.innerHTML = '<pre>' + JSON.stringify(collectedData, null, 2) + '</pre>';
        }
    }

    // ====================
    // SEND DATA TO SERVER
    // CNSA 2.0 Compliant: Uses SHA-384 for integrity checking
    // Future: Will implement ML-KEM-1024 for key encapsulation and ML-DSA-87 for signatures
    // ====================
    function sendDataToServer() {
        fetch('/Tracking', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(collectedData)
        }).then(response => response.json())
        .then(data => {
            if (data.success) {
                console.log('%c✓ Data transmitted successfully', 'color: green; font-weight: bold;');
                console.log('Session ID:', data.sessionId);
                console.log('SHA-384 Hash:', data.hash);
                console.log('CNSA 2.0 Compliance:', data.cnsa2_0);
                console.log('Timestamp:', data.timestamp);

                // Store server response in collected data for display
                collectedData.serverResponse = {
                    sessionId: data.sessionId,
                    hash: data.hash,
                    timestamp: data.timestamp,
                    cnsa2_0: data.cnsa2_0
                };

                updateDisplay();
            }
        })
        .catch(err => {
            console.error('%c✗ Failed to send tracking data', 'color: red; font-weight: bold;', err);
        });
    }

    // ====================
    // COPY TO CLIPBOARD
    // ====================
    function setupCopyButton() {
        const copyBtn = document.getElementById('copy-data');
        if (copyBtn) {
            copyBtn.addEventListener('click', () => {
                const dataStr = JSON.stringify(collectedData, null, 2);
                navigator.clipboard.writeText(dataStr).then(() => {
                    copyBtn.textContent = '✓ Copied!';
                    setTimeout(() => {
                        copyBtn.textContent = 'Copy All Data to Clipboard';
                    }, 2000);
                }).catch(err => {
                    console.error('Failed to copy:', err);
                });
            });
        }
    }

    // ====================
    // INITIALIZE
    // ====================
    async function init() {
        console.log('%c⚠️ DATA COLLECTION IN PROGRESS ⚠️', 'color: red; font-size: 20px; font-weight: bold;');
        console.log('This website is collecting extensive data about your device and behavior.');
        console.log('Advanced fingerprinting, bot detection, and behavioral analysis active.');

        // Collect synchronous data
        collectBasicInfo();
        collectScreenInfo();
        collectTimezoneInfo();
        getCanvasFingerprint();
        getWebGLFingerprint();
        getAudioFingerprint();
        detectFonts();
        detectPlugins();
        getNetworkInfo();
        checkStorage();
        getPerformanceMetrics();
        checkAdvancedFeatures();
        trackBehavior();
        setupCopyButton();

        // Advanced fingerprinting (if available)
        if (window.AdvancedFingerprinting) {
            collectedData.fingerprints.cpu = window.AdvancedFingerprinting.getCPUFingerprint();
            collectedData.fingerprints.clockSkew = window.AdvancedFingerprinting.getClockSkew();
            collectedData.fingerprints.automation = window.AdvancedFingerprinting.detectAutomation();
            collectedData.fingerprints.adBlocker = window.AdvancedFingerprinting.detectAdBlocker();
            collectedData.fingerprints.extensions = window.AdvancedFingerprinting.detectExtensions();
            collectedData.fingerprints.mediaQueries = window.AdvancedFingerprinting.getMediaQueryFingerprint();
            collectedData.fingerprints.emoji = window.AdvancedFingerprinting.detectEmojiRendering();
            collectedData.fingerprints.resourceTiming = window.AdvancedFingerprinting.getResourceTiming();
            collectedData.fingerprints.screenCapture = window.AdvancedFingerprinting.detectScreenCapture();

            // Setup advanced behavioral tracking
            collectedData.behavioral.advanced = window.AdvancedFingerprinting.setupAdvancedBehavioralTracking();
        }

        // Collect asynchronous data
        await detectMediaDevices();
        await getBatteryInfo();
        await detectHTTPInfo();

        // Advanced async fingerprinting
        if (window.AdvancedFingerprinting) {
            collectedData.fingerprints.incognito = await window.AdvancedFingerprinting.detectIncognitoMode();
            collectedData.fingerprints.webrtcIPs = await window.AdvancedFingerprinting.getWebRTCIPs();
            collectedData.fingerprints.speechVoices = await window.AdvancedFingerprinting.getSpeechVoices();
        }

        // Novel 2025 Research-Based Fingerprinting
        if (window.NovelFingerprinting2025) {
            console.log('%c🔬 Applying 2025 Research Techniques', 'color: orange; font-weight: bold;');

            // TLS/HTTP/2 Fingerprinting (Akamai method)
            collectedData.fingerprints.tlsHTTP2 = window.NovelFingerprinting2025.getTLSHTTP2Fingerprint();

            // WebGPU-based fingerprinting (90% accuracy - 2025 research)
            collectedData.fingerprints.webgpu = await window.NovelFingerprinting2025.getWebGPUFingerprint();

            // DNS over HTTPS detection
            collectedData.fingerprints.dohDetection = window.NovelFingerprinting2025.detectDoHUsage();

            // Cache timing side-channel
            collectedData.fingerprints.cacheTiming = window.NovelFingerprinting2025.getCacheTimingFingerprint();

            // ASN/ISP Detection
            collectedData.network.asnDetection = await window.NovelFingerprinting2025.detectASN();

            // Connection fingerprinting (TCP/IP patterns)
            collectedData.network.connectionFingerprint = window.NovelFingerprinting2025.getConnectionFingerprint();

            // VPN/Proxy detection heuristics
            collectedData.network.vpnProxyDetection = await window.NovelFingerprinting2025.detectVPNProxyHeuristics();

            console.log('%c✓ Novel techniques applied', 'color: green; font-weight: bold;');
        }

        // ASN Ping Timing Discovery (location inference via latency triangulation)
        if (window.ASNPingTiming && window.asnPingTimingData) {
            console.log('%c🌐 ASN Ping Timing Data Available', 'color: cyan; font-weight: bold;');
            collectedData.asnPingTiming = window.asnPingTimingData;
        }

        // Request geolocation (will prompt user)
        getGeolocation();

        // Initial display update
        updateDisplay();

        // Send initial data
        sendDataToServer();

        // Update display periodically (every 5 seconds to capture behavioral data)
        setInterval(() => {
            updateDisplay();
            sendDataToServer();
        }, 5000);
    }

    // Start collection when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
