// ASN Ping Timing Discovery
// Measures ping times to various ASNs to infer location
// Can correlate patterns across visits even when behind VPN

(function() {
    'use strict';

    // Major ASNs to ping (distributed globally for triangulation)
    const ASN_TARGETS = [
        // North America
        { asn: 'AS15169', name: 'Google LLC', country: 'US', targets: ['8.8.8.8', 'dns.google'], region: 'Global' },
        { asn: 'AS32934', name: 'Facebook', country: 'US', targets: ['31.13.64.1'], region: 'US-East' },
        { asn: 'AS8075', name: 'Microsoft', country: 'US', targets: ['20.190.128.0'], region: 'US-West' },
        { asn: 'AS16509', name: 'Amazon', country: 'US', targets: ['52.95.128.0'], region: 'US-East' },
        { asn: 'AS20057', name: 'Akamai', country: 'US', targets: ['23.185.0.0'], region: 'US-Central' },
        
        // Europe
        { asn: 'AS3320', name: 'Deutsche Telekom', country: 'DE', targets: ['194.25.0.1'], region: 'Central Europe' },
        { asn: 'AS3215', name: 'Orange', country: 'FR', targets: ['80.10.246.1'], region: 'Western Europe' },
        { asn: 'AS3352', name: 'Telefonica', country: 'ES', targets: ['213.97.0.1'], region: 'Southern Europe' },
        { asn: 'AS1299', name: 'Telia', country: 'SE', targets: ['213.155.0.1'], region: 'Northern Europe' },
        { asn: 'AS6830', name: 'Liberty Global', country: 'GB', targets: ['62.252.0.1'], region: 'UK' },
        
        // Asia Pacific
        { asn: 'AS4766', name: 'Korea Telecom', country: 'KR', targets: ['168.126.63.1'], region: 'Korea' },
        { asn: 'AS2914', name: 'NTT', country: 'JP', targets: ['202.12.27.33'], region: 'Japan' },
        { asn: 'AS4134', name: 'China Telecom', country: 'CN', targets: ['202.96.209.5'], region: 'China' },
        { asn: 'AS7473', name: 'Singtel', country: 'SG', targets: ['165.21.100.88'], region: 'Singapore' },
        { asn: 'AS4826', name: 'Telstra', country: 'AU', targets: ['139.130.4.5'], region: 'Australia' },
        
        // South America
        { asn: 'AS18881', name: 'Telefonica Brazil', country: 'BR', targets: ['200.160.2.3'], region: 'Brazil' },
        { asn: 'AS26615', name: 'Claro', country: 'AR', targets: ['200.89.70.1'], region: 'Argentina' },
        
        // Middle East & Africa
        { asn: 'AS36947', name: 'Etisalat', country: 'AE', targets: ['94.200.200.200'], region: 'UAE' },
        { asn: 'AS3741', name: 'MTN', country: 'ZA', targets: ['196.11.240.241'], region: 'South Africa' },
    ];

    // CDN endpoints for additional triangulation
    const CDN_TARGETS = [
        { name: 'Cloudflare', targets: ['1.1.1.1', '1.0.0.1'], asn: 'AS13335' },
        { name: 'Fastly', targets: ['151.101.1.140'], asn: 'AS54113' },
        { name: 'CloudFront', targets: ['13.32.0.0'], asn: 'AS16509' },
    ];

    /**
     * Measure ping time using Image loading technique
     * More reliable than fetch for cross-origin timing
     */
    function measurePingImage(target, timeout = 5000) {
        return new Promise((resolve) => {
            const startTime = performance.now();
            const img = new Image();
            let resolved = false;

            const cleanup = () => {
                if (!resolved) {
                    resolved = true;
                    img.onload = null;
                    img.onerror = null;
                }
            };

            img.onload = () => {
                const endTime = performance.now();
                const pingTime = endTime - startTime;
                cleanup();
                resolve({ success: true, time: pingTime, method: 'image' });
            };

            img.onerror = () => {
                const endTime = performance.now();
                const pingTime = endTime - startTime;
                cleanup();
                resolve({ success: false, time: pingTime, method: 'image', error: 'timeout' });
            };

            // Use a small 1x1 pixel image or favicon
            // For IP addresses, we'll use a generic endpoint
            const url = target.includes('.') && !target.includes('://') 
                ? `http://${target}/favicon.ico?t=${Date.now()}` 
                : `http://${target}/favicon.ico?t=${Date.now()}`;

            img.src = url;

            setTimeout(() => {
                if (!resolved) {
                    cleanup();
                    resolve({ success: false, time: timeout, method: 'image', error: 'timeout' });
                }
            }, timeout);
        });
    }

    /**
     * Measure ping time using Fetch API
     */
    async function measurePingFetch(target, timeout = 5000) {
        const startTime = performance.now();
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), timeout);

            const url = target.includes('://') ? target : `http://${target}/`;
            
            await fetch(url, {
                method: 'HEAD',
                mode: 'no-cors',
                signal: controller.signal,
                cache: 'no-store'
            });

            clearTimeout(timeoutId);
            const endTime = performance.now();
            const pingTime = endTime - startTime;
            return { success: true, time: pingTime, method: 'fetch' };
        } catch (error) {
            const endTime = performance.now();
            const pingTime = endTime - startTime;
            return { success: false, time: pingTime, method: 'fetch', error: error.name };
        }
    }

    /**
     * Measure ping time using WebSocket (most accurate but requires server support)
     */
    function measurePingWebSocket(target, timeout = 5000) {
        return new Promise((resolve) => {
            const startTime = performance.now();
            const ws = new WebSocket(`ws://${target}`);
            let resolved = false;

            const cleanup = () => {
                if (!resolved) {
                    resolved = true;
                    ws.close();
                }
            };

            ws.onopen = () => {
                const endTime = performance.now();
                const pingTime = endTime - startTime;
                cleanup();
                resolve({ success: true, time: pingTime, method: 'websocket' });
            };

            ws.onerror = () => {
                const endTime = performance.now();
                const pingTime = endTime - startTime;
                cleanup();
                resolve({ success: false, time: pingTime, method: 'websocket', error: 'connection_failed' });
            };

            setTimeout(() => {
                if (!resolved) {
                    cleanup();
                    resolve({ success: false, time: timeout, method: 'websocket', error: 'timeout' });
                }
            }, timeout);
        });
    }

    /**
     * Measure ping to a target using multiple methods
     */
    async function measurePing(target, attempts = 3) {
        const results = [];
        
        for (let i = 0; i < attempts; i++) {
            // Try image method first (most reliable for cross-origin)
            const result = await measurePingImage(target, 3000);
            if (result.success) {
                results.push(result.time);
            }
            
            // Small delay between attempts
            await new Promise(resolve => setTimeout(resolve, 100));
        }

        if (results.length === 0) {
            return {
                success: false,
                average: null,
                min: null,
                max: null,
                attempts: attempts,
                successful: 0
            };
        }

        const sum = results.reduce((a, b) => a + b, 0);
        const average = sum / results.length;
        const min = Math.min(...results);
        const max = Math.max(...results);
        
        // Calculate jitter (standard deviation)
        const variance = results.reduce((acc, val) => acc + Math.pow(val - average, 2), 0) / results.length;
        const jitter = Math.sqrt(variance);

        return {
            success: true,
            average: Math.round(average * 100) / 100,
            min: Math.round(min * 100) / 100,
            max: Math.round(max * 100) / 100,
            attempts: attempts,
            successful: results.length,
            jitter: Math.round(jitter * 100) / 100,
            times: results
        };
    }

    /**
     * Measure ping times to all ASN targets
     */
    async function measureAllASNPings() {
        const results = [];
        const startTime = Date.now();

        // Measure pings to major ASNs (sample subset for performance)
        const targetsToMeasure = ASN_TARGETS.slice(0, 10); // Measure top 10 for performance

        for (const asnTarget of targetsToMeasure) {
            for (const target of asnTarget.targets.slice(0, 1)) { // Use first target per ASN
                try {
                    const pingResult = await measurePing(target, 2); // 2 attempts per target
                    
                    results.push({
                        asn: asnTarget.asn,
                        asnName: asnTarget.name,
                        country: asnTarget.country,
                        region: asnTarget.region,
                        target: target,
                        targetType: 'IP',
                        ...pingResult,
                        measuredAt: new Date().toISOString()
                    });

                    // Small delay between ASNs
                    await new Promise(resolve => setTimeout(resolve, 200));
                } catch (error) {
                    console.warn(`Failed to measure ping to ${target}:`, error);
                }
            }
        }

        // Measure CDN endpoints
        for (const cdn of CDN_TARGETS.slice(0, 3)) {
            for (const target of cdn.targets.slice(0, 1)) {
                try {
                    const pingResult = await measurePing(target, 2);
                    
                    results.push({
                        asn: cdn.asn,
                        asnName: cdn.name,
                        country: 'Global',
                        region: 'CDN',
                        target: target,
                        targetType: 'CDN',
                        ...pingResult,
                        measuredAt: new Date().toISOString()
                    });

                    await new Promise(resolve => setTimeout(resolve, 200));
                } catch (error) {
                    console.warn(`Failed to measure CDN ping to ${target}:`, error);
                }
            }
        }

        const endTime = Date.now();
        const totalTime = endTime - startTime;

        return {
            measurements: results,
            totalTargets: results.length,
            successfulMeasurements: results.filter(r => r.success).length,
            measurementDuration: totalTime,
            timestamp: new Date().toISOString()
        };
    }

    /**
     * Create ping pattern signature for correlation
     */
    function createPingPattern(measurements) {
        // Normalize ping times relative to fastest ping
        const successfulMeasurements = measurements.filter(m => m.success && m.average !== null);
        
        if (successfulMeasurements.length === 0) {
            return null;
        }

        const fastestPing = Math.min(...successfulMeasurements.map(m => m.average));
        
        // Create normalized pattern (relative times)
        const pattern = successfulMeasurements.map(m => ({
            asn: m.asn,
            normalizedTime: m.average / fastestPing, // Relative to fastest
            absoluteTime: m.average,
            country: m.country,
            region: m.region
        }));

        // Sort by normalized time
        pattern.sort((a, b) => a.normalizedTime - b.normalizedTime);

        return {
            pattern: pattern,
            fastestASN: pattern[0].asn,
            fastestTime: fastestPing,
            patternHash: null // Will be computed server-side
        };
    }

    // Export functions for use in tracking.js
    window.ASNPingTiming = {
        measureAllASNPings: measureAllASNPings,
        createPingPattern: createPingPattern,
        ASN_TARGETS: ASN_TARGETS,
        CDN_TARGETS: CDN_TARGETS
    };

    // Auto-measure on load (can be disabled)
    if (typeof window.autoMeasureASNPings === 'undefined' || window.autoMeasureASNPings !== false) {
        // Measure asynchronously after page load
        window.addEventListener('load', () => {
            setTimeout(async () => {
                try {
                    const pingData = await measureAllASNPings();
                    const pattern = createPingPattern(pingData.measurements);
                    
                    // Store in global for tracking.js to pick up
                    window.asnPingTimingData = {
                        measurements: pingData,
                        pattern: pattern
                    };
                    
                    console.log('ASN Ping Timing completed:', {
                        targets: pingData.totalTargets,
                        successful: pingData.successfulMeasurements,
                        duration: pingData.measurementDuration + 'ms'
                    });
                } catch (error) {
                    console.error('ASN Ping Timing error:', error);
                }
            }, 2000); // Wait 2 seconds after page load
        });
    }
})();

