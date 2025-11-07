// Advanced Passive Fingerprinting Extensions
// No user interaction required - all passive collection

(function() {
    'use strict';

    // ====================
    // CPU BENCHMARKING & PERFORMANCE FINGERPRINTING
    // ====================
    function getCPUFingerprint() {
        const start = performance.now();
        let result = 0;

        // CPU-intensive operation for benchmarking
        for (let i = 0; i < 100000; i++) {
            result += Math.sqrt(i) * Math.sin(i) * Math.cos(i);
        }

        const cpuTime = performance.now() - start;

        // Test different operations
        const tests = {
            mathOperations: cpuTime,
            stringOperations: measureStringOps(),
            arrayOperations: measureArrayOps(),
            cryptoOperations: measureCryptoOps()
        };

        return {
            totalTime: cpuTime,
            operations: tests,
            performanceScore: calculatePerformanceScore(tests)
        };
    }

    function measureStringOps() {
        const start = performance.now();
        let str = '';
        for (let i = 0; i < 10000; i++) {
            str += 'a';
            str = str.substring(0, str.length - 1);
        }
        return performance.now() - start;
    }

    function measureArrayOps() {
        const start = performance.now();
        const arr = [];
        for (let i = 0; i < 10000; i++) {
            arr.push(i);
            arr.sort();
        }
        return performance.now() - start;
    }

    function measureCryptoOps() {
        const start = performance.now();
        for (let i = 0; i < 1000; i++) {
            Math.random();
        }
        return performance.now() - start;
    }

    function calculatePerformanceScore(tests) {
        return Object.values(tests).reduce((a, b) => a + b, 0);
    }

    // ====================
    // CLOCK SKEW & TIMING
    // ====================
    function getClockSkew() {
        const dateNow = Date.now();
        const perfNow = performance.now() + performance.timeOrigin;
        const skew = dateNow - perfNow;

        return {
            skew: skew,
            dateNow: dateNow,
            performanceNow: perfNow,
            timeOrigin: performance.timeOrigin,
            monotonic: performance.now()
        };
    }

    // ====================
    // BROWSER AUTOMATION DETECTION
    // ====================
    function detectAutomation() {
        const automation = {
            webdriver: navigator.webdriver || false,
            phantom: !!(window.phantom || window._phantom || window.callPhantom),
            nightmare: !!window.__nightmare,
            selenium: !!(window.document.documentElement.getAttribute('selenium') ||
                        window.document.documentElement.getAttribute('webdriver') ||
                        window.document.documentElement.getAttribute('driver')),
            chromeDriverPresent: !!(window.document.$cdc_ || window.document.$chrome_asyncScriptInfo),
            headlessChrome: detectHeadlessChrome(),
            automationFlags: {
                webdriverProperty: 'webdriver' in navigator,
                permissionsAPI: detectPermissionsAPI(),
                pluginsLength: navigator.plugins.length === 0,
                languagesEmpty: navigator.languages.length === 0,
                notificationPermission: checkNotificationPermission(),
                chromeRuntime: !!(window.chrome && window.chrome.runtime)
            }
        };

        return automation;
    }

    function detectHeadlessChrome() {
        // Multiple headless detection techniques
        return (
            !!(window.chrome && !window.chrome.runtime) ||
            (navigator.userAgent.indexOf('HeadlessChrome') !== -1) ||
            (!navigator.plugins.length && navigator.userAgent.indexOf('Chrome') !== -1)
        );
    }

    function detectPermissionsAPI() {
        if (!navigator.permissions) return 'not-supported';

        // Headless browsers often have quirks with permissions API
        try {
            const permissionStatus = navigator.permissions.query({ name: 'notifications' });
            return 'supported';
        } catch (e) {
            return 'error';
        }
    }

    function checkNotificationPermission() {
        if (!('Notification' in window)) return 'not-supported';
        return Notification.permission;
    }

    // ====================
    // INCOGNITO/PRIVATE MODE DETECTION
    // ====================
    async function detectIncognitoMode() {
        return new Promise((resolve) => {
            // Test FileSystem API (blocked in incognito in some browsers)
            if ('storage' in navigator && 'estimate' in navigator.storage) {
                navigator.storage.estimate().then(estimate => {
                    resolve({
                        detected: estimate.quota < 120000000, // Typically much smaller in incognito
                        quota: estimate.quota,
                        usage: estimate.usage,
                        method: 'storage-estimate'
                    });
                });
            } else if (window.webkitRequestFileSystem) {
                window.webkitRequestFileSystem(
                    window.TEMPORARY,
                    1,
                    () => resolve({ detected: false, method: 'filesystem' }),
                    () => resolve({ detected: true, method: 'filesystem' })
                );
            } else {
                resolve({ detected: 'unknown', method: 'none' });
            }
        });
    }

    // ====================
    // AD BLOCKER DETECTION
    // ====================
    function detectAdBlocker() {
        // Create a bait element that ad blockers typically block
        const bait = document.createElement('div');
        bait.className = 'ad-banner advertisement ads ad-placement';
        bait.style.position = 'absolute';
        bait.style.top = '-1px';
        bait.style.height = '1px';
        document.body.appendChild(bait);

        const detected = bait.offsetHeight === 0 ||
                        bait.clientHeight === 0 ||
                        window.getComputedStyle(bait).display === 'none' ||
                        window.getComputedStyle(bait).visibility === 'hidden';

        document.body.removeChild(bait);

        return {
            detected: detected,
            method: 'element-blocking'
        };
    }

    // ====================
    // BROWSER EXTENSION DETECTION
    // ====================
    function detectExtensions() {
        const extensions = {
            grammarly: !!document.querySelector('[data-gr-ext]'),
            lastPass: !!document.querySelector('[data-lastpass-icon-root]'),
            honey: !!document.querySelector('#honey'),
            metamask: !!(window.ethereum || window.web3),
            colorblind: detectColorblindExtension(),
            darkReader: !!document.querySelector('.darkreader'),
            adBlock: detectAdBlocker().detected
        };

        return extensions;
    }

    function detectColorblindExtension() {
        // Colorblind extensions often modify SVG filters
        const svgFilters = document.querySelectorAll('svg defs filter');
        return svgFilters.length > 0;
    }

    // ====================
    // ENHANCED BEHAVIORAL TRACKING
    // ====================
    function setupAdvancedBehavioralTracking() {
        const behavioral = {
            copyPaste: [],
            contextMenu: [],
            tabSwitches: 0,
            windowResizes: [],
            orientationChanges: [],
            touchGestures: [],
            readingSpeed: {},
            attentionMetrics: {},
            idleTime: 0,
            lastActivity: Date.now()
        };

        // Copy detection
        document.addEventListener('copy', (e) => {
            behavioral.copyPaste.push({
                type: 'copy',
                selection: window.getSelection().toString().substring(0, 100),
                timestamp: Date.now()
            });
        });

        // Paste detection
        document.addEventListener('paste', (e) => {
            behavioral.copyPaste.push({
                type: 'paste',
                timestamp: Date.now()
            });
        });

        // Right-click/context menu
        document.addEventListener('contextmenu', (e) => {
            behavioral.contextMenu.push({
                x: e.clientX,
                y: e.clientY,
                target: e.target.tagName,
                timestamp: Date.now()
            });
        });

        // Tab/window focus changes (indicates multitasking or tab switching)
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                behavioral.tabSwitches++;
            }
        });

        // Window resize tracking
        let resizeTimeout;
        window.addEventListener('resize', (e) => {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(() => {
                behavioral.windowResizes.push({
                    width: window.innerWidth,
                    height: window.innerHeight,
                    timestamp: Date.now()
                });
            }, 200);
        });

        // Orientation change (mobile)
        window.addEventListener('orientationchange', () => {
            behavioral.orientationChanges.push({
                orientation: screen.orientation ? screen.orientation.type : window.orientation,
                timestamp: Date.now()
            });
        });

        // Touch gestures (mobile)
        let touchStart = null;
        document.addEventListener('touchstart', (e) => {
            touchStart = {
                x: e.touches[0].clientX,
                y: e.touches[0].clientY,
                time: Date.now()
            };
        });

        document.addEventListener('touchend', (e) => {
            if (touchStart) {
                const touchEnd = {
                    x: e.changedTouches[0].clientX,
                    y: e.changedTouches[0].clientY,
                    time: Date.now()
                };

                const deltaX = touchEnd.x - touchStart.x;
                const deltaY = touchEnd.y - touchStart.y;
                const deltaTime = touchEnd.time - touchStart.time;

                behavioral.touchGestures.push({
                    deltaX: deltaX,
                    deltaY: deltaY,
                    duration: deltaTime,
                    velocity: Math.sqrt(deltaX * deltaX + deltaY * deltaY) / deltaTime,
                    timestamp: Date.now()
                });
            }
        });

        // Idle time tracking
        let idleTimer;
        function resetIdleTimer() {
            behavioral.idleTime = Date.now() - behavioral.lastActivity;
            behavioral.lastActivity = Date.now();
        }

        ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart'].forEach(event => {
            document.addEventListener(event, resetIdleTimer, true);
        });

        return behavioral;
    }

    // ====================
    // CSS MEDIA QUERIES FINGERPRINTING
    // ====================
    function getMediaQueryFingerprint() {
        const queries = {
            'prefers-reduced-motion': window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            'prefers-color-scheme-dark': window.matchMedia('(prefers-color-scheme: dark)').matches,
            'prefers-color-scheme-light': window.matchMedia('(prefers-color-scheme: light)').matches,
            'prefers-contrast-high': window.matchMedia('(prefers-contrast: high)').matches,
            'prefers-contrast-low': window.matchMedia('(prefers-contrast: low)').matches,
            'prefers-reduced-transparency': window.matchMedia('(prefers-reduced-transparency: reduce)').matches,
            'forced-colors': window.matchMedia('(forced-colors: active)').matches,
            'inverted-colors': window.matchMedia('(inverted-colors: inverted)').matches,
            'hover': window.matchMedia('(hover: hover)').matches,
            'pointer-fine': window.matchMedia('(pointer: fine)').matches,
            'pointer-coarse': window.matchMedia('(pointer: coarse)').matches,
            'any-hover': window.matchMedia('(any-hover: hover)').matches,
            'any-pointer-fine': window.matchMedia('(any-pointer: fine)').matches,
            'monochrome': window.matchMedia('(monochrome)').matches,
            'color-gamut-srgb': window.matchMedia('(color-gamut: srgb)').matches,
            'color-gamut-p3': window.matchMedia('(color-gamut: p3)').matches,
            'color-gamut-rec2020': window.matchMedia('(color-gamut: rec2020)').matches
        };

        return queries;
    }

    // ====================
    // WEBRTC IP LEAK (passive, no getUserMedia)
    // ====================
    function getWebRTCIPs() {
        return new Promise((resolve) => {
            const ips = [];

            if (!window.RTCPeerConnection) {
                resolve({ error: 'RTCPeerConnection not supported' });
                return;
            }

            try {
                const pc = new RTCPeerConnection({
                    iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
                });

                pc.createDataChannel('');
                pc.createOffer().then(offer => pc.setLocalDescription(offer));

                pc.onicecandidate = (ice) => {
                    if (!ice || !ice.candidate || !ice.candidate.candidate) {
                        resolve(ips.length ? ips : { error: 'No IPs found' });
                        pc.close();
                        return;
                    }

                    const candidate = ice.candidate.candidate;
                    const ipRegex = /([0-9]{1,3}\.){3}[0-9]{1,3}/;
                    const ipMatch = ipRegex.exec(candidate);

                    if (ipMatch && !ips.includes(ipMatch[0])) {
                        ips.push(ipMatch[0]);
                    }
                };

                setTimeout(() => {
                    resolve(ips.length ? ips : { error: 'Timeout' });
                    pc.close();
                }, 2000);

            } catch (e) {
                resolve({ error: e.message });
            }
        });
    }

    // ====================
    // EMOJI/UNICODE RENDERING DETECTION
    // ====================
    function detectEmojiRendering() {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        canvas.width = 50;
        canvas.height = 50;

        const emojis = ['😀', '🏴‍☠️', '👨‍👩‍👧‍👦', '🇺🇸'];
        const results = {};

        emojis.forEach(emoji => {
            ctx.clearRect(0, 0, 50, 50);
            ctx.font = '40px Arial';
            ctx.fillText(emoji, 0, 40);
            const imageData = ctx.getImageData(0, 0, 50, 50);
            const hash = simpleHash(Array.from(imageData.data).join(','));
            results[emoji] = hash;
        });

        return results;
    }

    // ====================
    // MEMORY & RESOURCE MONITORING
    // ====================
    function getResourceTiming() {
        if (!performance.getEntriesByType) return { error: 'not supported' };

        const resources = performance.getEntriesByType('resource').map(r => ({
            name: r.name.substring(r.name.lastIndexOf('/') + 1),
            duration: r.duration,
            size: r.transferSize,
            type: r.initiatorType,
            protocol: r.nextHopProtocol
        }));

        return {
            count: resources.length,
            totalDuration: resources.reduce((sum, r) => sum + r.duration, 0),
            totalSize: resources.reduce((sum, r) => sum + (r.size || 0), 0),
            protocols: [...new Set(resources.map(r => r.protocol))],
            types: [...new Set(resources.map(r => r.type))]
        };
    }

    // ====================
    // SPEECH SYNTHESIS VOICES (unique per system)
    // ====================
    function getSpeechVoices() {
        if (!window.speechSynthesis) return { error: 'not supported' };

        return new Promise((resolve) => {
            const voices = speechSynthesis.getVoices();
            if (voices.length) {
                resolve(voices.map(v => ({
                    name: v.name,
                    lang: v.lang,
                    default: v.default,
                    localService: v.localService
                })));
            } else {
                speechSynthesis.onvoiceschanged = () => {
                    const voices = speechSynthesis.getVoices();
                    resolve(voices.map(v => ({
                        name: v.name,
                        lang: v.lang,
                        default: v.default,
                        localService: v.localService
                    })));
                };

                setTimeout(() => resolve({ error: 'timeout' }), 1000);
            }
        });
    }

    // ====================
    // SCREEN CAPTURE DETECTION
    // ====================
    function detectScreenCapture() {
        // Some browsers expose this when screen recording is active
        return {
            mediaDevicesActive: navigator.mediaDevices ? 'available' : 'not-available',
            getDisplayMedia: !!(navigator.mediaDevices && navigator.mediaDevices.getDisplayMedia),
            screenRecordingAPI: 'mediaRecorder' in window
        };
    }

    // ====================
    // UTILITY
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
    // EXPORT TO MAIN TRACKING
    // ====================
    window.AdvancedFingerprinting = {
        getCPUFingerprint,
        getClockSkew,
        detectAutomation,
        detectIncognitoMode,
        detectAdBlocker,
        detectExtensions,
        setupAdvancedBehavioralTracking,
        getMediaQueryFingerprint,
        getWebRTCIPs,
        detectEmojiRendering,
        getResourceTiming,
        getSpeechVoices,
        detectScreenCapture
    };

})();
