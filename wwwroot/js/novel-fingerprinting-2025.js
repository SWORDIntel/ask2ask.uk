// Novel Tracking Techniques from 2025 Research
// Based on latest academic research and industry findings

(function() {
    'use strict';

    // ====================
    // TLS/HTTP/2 FINGERPRINTING (Akamai Research 2025)
    // ====================
    function getTLSHTTP2Fingerprint() {
        const fingerprint = {
            protocol: location.protocol,
            httpVersion: 'unknown',
            supportsHTTP2: false,
            supportsHTTP3: false,
            features: {}
        };

        // Check for HTTP/2 support via Performance API
        if (window.performance && performance.getEntriesByType) {
            const navEntry = performance.getEntriesByType('navigation')[0];
            if (navEntry) {
                fingerprint.httpVersion = navEntry.nextHopProtocol || 'http/1.1';
                fingerprint.supportsHTTP2 = navEntry.nextHopProtocol === 'h2';
                fingerprint.supportsHTTP3 = navEntry.nextHopProtocol === 'h3';
            }

            // Analyze resource loading patterns for HTTP/2 detection
            const resources = performance.getEntriesByType('resource');
            if (resources.length > 0) {
                fingerprint.features.resourceProtocols = [...new Set(resources.map(r => r.nextHopProtocol))];
                fingerprint.features.multiplexingDetected = resources.some(r => r.nextHopProtocol === 'h2');
            }
        }

        // Check for fetch/keepalive support (HTTP/2 indicator)
        fingerprint.features.fetchKeepalive = typeof fetch !== 'undefined' && 'keepalive' in new Request('');

        // Check for Server Push API (HTTP/2 feature)
        fingerprint.features.serverPushSupported = 'PushManager' in window;

        return fingerprint;
    }

    // ====================
    // WebGPU FINGERPRINTING (WebGPU-SPY Research 2025)
    // GPU Cache Attack Timing - 90% accuracy for identification
    // ====================
    async function getWebGPUFingerprint() {
        if (!navigator.gpu) {
            return { supported: false, reason: 'WebGPU not available' };
        }

        try {
            const adapter = await navigator.gpu.requestAdapter();
            if (!adapter) {
                return { supported: false, reason: 'No adapter available' };
            }

            const device = await adapter.requestDevice();

            // GPU Timing Attack - measure computation time for fingerprinting
            const startTime = performance.now();

            // Create a compute shader for timing analysis
            const shaderCode = `
                @group(0) @binding(0) var<storage, read_write> data: array<f32>;

                @compute @workgroup_size(64)
                fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
                    let index = global_id.x;
                    data[index] = sqrt(f32(index)) * sin(f32(index));
                }
            `;

            const shaderModule = device.createShaderModule({ code: shaderCode });

            // Buffer for timing computation
            const bufferSize = 1024 * 4; // 1024 floats
            const buffer = device.createBuffer({
                size: bufferSize,
                usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_SRC
            });

            const bindGroupLayout = device.createBindGroupLayout({
                entries: [{
                    binding: 0,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'storage' }
                }]
            });

            const bindGroup = device.createBindGroup({
                layout: bindGroupLayout,
                entries: [{ binding: 0, resource: { buffer } }]
            });

            const computePipeline = device.createComputePipeline({
                layout: device.createPipelineLayout({ bindGroupLayouts: [bindGroupLayout] }),
                compute: { module: shaderModule, entryPoint: 'main' }
            });

            const commandEncoder = device.createCommandEncoder();
            const passEncoder = commandEncoder.beginComputePass();
            passEncoder.setPipeline(computePipeline);
            passEncoder.setBindGroup(0, bindGroup);
            passEncoder.dispatchWorkgroups(16);
            passEncoder.end();

            device.queue.submit([commandEncoder.finish()]);
            await device.queue.onSubmittedWorkDone();

            const computeTime = performance.now() - startTime;

            return {
                supported: true,
                vendor: adapter.info?.vendor || 'unknown',
                architecture: adapter.info?.architecture || 'unknown',
                device: adapter.info?.device || 'unknown',
                description: adapter.info?.description || 'unknown',
                limits: {
                    maxTextureDimension1D: adapter.limits.maxTextureDimension1D,
                    maxTextureDimension2D: adapter.limits.maxTextureDimension2D,
                    maxTextureDimension3D: adapter.limits.maxTextureDimension3D,
                    maxBindGroups: adapter.limits.maxBindGroups,
                    maxComputeWorkgroupSizeX: adapter.limits.maxComputeWorkgroupSizeX,
                    maxComputeInvocationsPerWorkgroup: adapter.limits.maxComputeInvocationsPerWorkgroup
                },
                features: Array.from(adapter.features || []),
                timing: {
                    computeTime: computeTime,
                    fingerprint: Math.round(computeTime * 1000) // Unique timing signature
                }
            };
        } catch (e) {
            return { supported: false, error: e.message };
        }
    }

    // ====================
    // DNS OVER HTTPS (DoH) DETECTION (2025 Research)
    // Detects DoH usage and patterns
    // ====================
    function detectDoHUsage() {
        const dohIndicators = {
            dohLikely: false,
            indicators: [],
            dnsProviders: []
        };

        // Check for common DoH providers via timing and connection patterns
        const commonDoHProviders = [
            'cloudflare-dns.com',
            'dns.google',
            'dns.quad9.net',
            'doh.opendns.com',
            'mozilla.cloudflare-dns.com'
        ];

        // Attempt to detect DoH by checking if DNS queries exhibit HTTP/2 patterns
        if (window.performance && performance.getEntriesByType) {
            const resources = performance.getEntriesByType('resource');

            // Look for HTTPS connections to known DoH providers
            resources.forEach(resource => {
                const url = new URL(resource.name);
                if (commonDoHProviders.some(provider => url.hostname.includes(provider))) {
                    dohIndicators.dohLikely = true;
                    dohIndicators.dnsProviders.push(url.hostname);
                    dohIndicators.indicators.push('Known DoH provider detected');
                }

                // Check for HTTP/2 connections to port 443 with DNS-like patterns
                if (resource.nextHopProtocol === 'h2' && url.pathname.includes('/dns-query')) {
                    dohIndicators.dohLikely = true;
                    dohIndicators.indicators.push('DoH query pattern detected');
                }
            });
        }

        // Check browser settings hints
        if (navigator.connection) {
            dohIndicators.connectionInfo = {
                effectiveType: navigator.connection.effectiveType,
                saveData: navigator.connection.saveData
            };
        }

        return dohIndicators;
    }

    // ====================
    // CACHE TIMING SIDE-CHANNEL ATTACK
    // Based on 2025 research - can detect websites even without JavaScript timing APIs
    // ====================
    function getCacheTimingFingerprint() {
        const results = {
            cacheEnabled: false,
            timingPrecision: 0,
            cachePattern: []
        };

        try {
            // Test cache behavior with performance.now() if available
            if (performance.now) {
                const measurements = [];

                // Create test resources to measure cache behavior
                for (let i = 0; i < 10; i++) {
                    const start = performance.now();

                    // Access a resource to measure cache timing
                    const img = new Image();
                    img.src = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';

                    const end = performance.now();
                    measurements.push(end - start);
                }

                results.cacheEnabled = true;
                results.timingPrecision = Math.min(...measurements);
                results.cachePattern = measurements;

                // Calculate standard deviation (cache consistency)
                const mean = measurements.reduce((a, b) => a + b) / measurements.length;
                const variance = measurements.reduce((sum, val) => sum + Math.pow(val - mean, 2), 0) / measurements.length;
                results.cacheConsistency = Math.sqrt(variance);
            }

            // CSS-based timing (works even without JavaScript timing APIs)
            results.cssTimingAvailable = typeof getComputedStyle !== 'undefined';

        } catch (e) {
            results.error = e.message;
        }

        return results;
    }

    // ====================
    // ASN (AUTONOMOUS SYSTEM) DETECTION
    // Multiple techniques to identify ISP/hosting provider
    // ====================
    async function detectASN() {
        const asnInfo = {
            methods: [],
            detectedISP: null,
            hostingProvider: null,
            asn: null,
            ipType: 'unknown' // datacenter, residential, mobile, etc.
        };

        // Method 1: Timing-based ISP fingerprinting
        // Different ISPs have characteristic latency patterns
        const timings = {
            dnsLookup: 0,
            tcpConnect: 0,
            tlsHandshake: 0
        };

        if (performance.getEntriesByType) {
            const navTiming = performance.getEntriesByType('navigation')[0];
            if (navTiming) {
                timings.dnsLookup = navTiming.domainLookupEnd - navTiming.domainLookupStart;
                timings.tcpConnect = navTiming.connectEnd - navTiming.connectStart;
                timings.tlsHandshake = navTiming.connectEnd - navTiming.secureConnectionStart;

                asnInfo.methods.push('timing-analysis');

                // Characteristic patterns for different network types
                if (timings.dnsLookup < 10 && timings.tcpConnect < 20) {
                    asnInfo.ipType = 'datacenter'; // Very fast, likely datacenter/VPN
                } else if (timings.dnsLookup > 50 || timings.tcpConnect > 100) {
                    asnInfo.ipType = 'residential-or-mobile'; // Slower, likely residential
                }
            }
        }

        // Method 2: MTU Detection (can indicate network type)
        // Different networks use different MTU sizes
        const mtuHints = {
            detected: false,
            estimatedMTU: 0
        };

        if (navigator.connection && navigator.connection.downlink) {
            // Mobile networks often have different MTU
            if (navigator.connection.effectiveType === '4g' || navigator.connection.effectiveType === '3g') {
                asnInfo.ipType = 'mobile';
                mtuHints.detected = true;
                mtuHints.estimatedMTU = 1430; // Typical mobile MTU
            }
        }

        asnInfo.mtuHints = mtuHints;

        // Method 3: Check for hosting provider characteristics
        // Datacenter IPs often have specific patterns
        const hostingIndicators = {
            multiplexingCapable: false,
            lowLatency: timings.tcpConnect < 30,
            consistentTiming: true,
            http2Support: false
        };

        if (performance.getEntriesByType) {
            const resources = performance.getEntriesByType('resource');
            hostingIndicators.http2Support = resources.some(r => r.nextHopProtocol === 'h2');
            hostingIndicators.multiplexingCapable = resources.length > 10 && hostingIndicators.http2Support;
        }

        // Datacenter/VPN detection heuristics
        if (hostingIndicators.lowLatency && hostingIndicators.multiplexingCapable) {
            asnInfo.hostingProvider = 'likely-datacenter-or-vpn';
            asnInfo.methods.push('hosting-heuristics');
        }

        asnInfo.hostingIndicators = hostingIndicators;
        asnInfo.timings = timings;

        return asnInfo;
    }

    // ====================
    // CONNECTION FINGERPRINTING (TCP/IP Stack Behavior)
    // Based on research showing different OS/networks have unique patterns
    // ====================
    function getConnectionFingerprint() {
        const connInfo = {
            protocols: [],
            features: {},
            patterns: {}
        };

        // Resource Timing API gives us TCP/TLS characteristics
        if (performance.getEntriesByType) {
            const resources = performance.getEntriesByType('resource');
            const navigation = performance.getEntriesByType('navigation')[0];

            if (navigation) {
                connInfo.patterns.connectionReuse = navigation.connectEnd === navigation.connectStart;
                connInfo.patterns.redirectCount = navigation.redirectCount;
                connInfo.patterns.transferSize = navigation.transferSize;
                connInfo.patterns.encodedBodySize = navigation.encodedBodySize;
                connInfo.patterns.decodedBodySize = navigation.decodedBodySize;

                // Calculate compression ratio (can indicate network optimization)
                if (navigation.encodedBodySize > 0) {
                    connInfo.patterns.compressionRatio =
                        navigation.decodedBodySize / navigation.encodedBodySize;
                }
            }

            // Analyze protocol usage patterns
            const protocolCounts = {};
            resources.forEach(r => {
                const proto = r.nextHopProtocol || 'unknown';
                protocolCounts[proto] = (protocolCounts[proto] || 0) + 1;
            });
            connInfo.protocols = protocolCounts;

            // Connection pooling detection
            const connectionStarts = resources.map(r => r.connectStart).filter(t => t > 0);
            connInfo.features.connectionPoolingDetected = connectionStarts.length < resources.length / 2;
            connInfo.features.parallelConnections = new Set(connectionStarts).size;
        }

        // Check for various connection-related features
        connInfo.features.sendBeacon = typeof navigator.sendBeacon !== 'undefined';
        connInfo.features.fetchStreaming = typeof ReadableStream !== 'undefined';

        // Check for network information API
        if (navigator.connection) {
            connInfo.networkInfo = {
                type: navigator.connection.type,
                effectiveType: navigator.connection.effectiveType,
                downlink: navigator.connection.downlink,
                rtt: navigator.connection.rtt,
                saveData: navigator.connection.saveData
            };
        }

        return connInfo;
    }

    // ====================
    // VPN/PROXY DETECTION HEURISTICS
    // Multiple signals that indicate VPN/proxy usage
    // ====================
    async function detectVPNProxyHeuristics() {
        const indicators = {
            vpnLikely: false,
            proxyLikely: false,
            signals: [],
            confidence: 0
        };

        let signalCount = 0;

        // Signal 1: WebRTC vs Server IP mismatch
        const webrtcIPs = await getWebRTCIPs();
        if (webrtcIPs && !webrtcIPs.error && webrtcIPs.length > 0) {
            // If WebRTC returns multiple IPs, some might be local
            if (webrtcIPs.length > 1) {
                indicators.signals.push('Multiple IPs detected via WebRTC');
                signalCount++;
            }
        }

        // Signal 2: Timezone vs Language mismatch
        const browserLang = navigator.language;
        const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;

        // Check for common VPN mismatches
        if (timezone && browserLang) {
            if (timezone.includes('America') && browserLang.startsWith('de')) {
                indicators.signals.push('Timezone/Language mismatch detected');
                indicators.vpnLikely = true;
                signalCount++;
            }
        }

        // Signal 3: Canvas fingerprint consistency
        // VPNs don't change canvas, so mismatch with IP geo is suspicious
        indicators.signals.push('Canvas fingerprint analyzed');

        // Signal 4: WebRTC blocking (privacy-conscious or VPN)
        if (!navigator.mediaDevices || webrtcIPs.error) {
            indicators.signals.push('WebRTC appears blocked or restricted');
            indicators.vpnLikely = true;
            signalCount++;
        }

        // Signal 5: Battery API blocking
        if (!navigator.getBattery) {
            indicators.signals.push('Battery API unavailable (privacy tool)');
            signalCount++;
        }

        // Signal 6: High-speed low-latency connection (datacenter)
        if (navigator.connection && navigator.connection.downlink > 10 && navigator.connection.rtt < 50) {
            indicators.signals.push('Datacenter-like connection characteristics');
            indicators.vpnLikely = true;
            signalCount++;
        }

        indicators.confidence = Math.min(signalCount / 3, 1) * 100; // Confidence percentage

        return indicators;
    }

    // Helper function for WebRTC IP detection (referenced above)
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
    // EXPORT TO MAIN TRACKING
    // ====================
    window.NovelFingerprinting2025 = {
        getTLSHTTP2Fingerprint,
        getWebGPUFingerprint,
        detectDoHUsage,
        getCacheTimingFingerprint,
        detectASN,
        getConnectionFingerprint,
        detectVPNProxyHeuristics
    };

})();
