# ASN Ping Timing Discovery
## Location Inference via Network Latency Triangulation

## Overview

ASN Ping Timing Discovery measures network latency to various Autonomous System Numbers (ASNs) distributed globally to infer client location. This technique can correlate location patterns across multiple visits, even when users are behind VPNs or proxies.

## How It Works

### 1. Ping Measurement
- Client-side JavaScript measures ping times to multiple ASN endpoints
- Targets include major ISPs, cloud providers, and CDNs distributed globally
- Uses multiple measurement methods: Image loading, Fetch API, WebSocket

### 2. Pattern Creation
- Normalizes ping times relative to fastest ping
- Creates a "ping fingerprint" unique to geographic location
- Pattern includes: ASN identifiers, normalized latencies, geographic metadata

### 3. Correlation Across Visits
- Stores ping patterns with SHA-384 hash (CNSA 2.0 compliant)
- Compares patterns across multiple visits for same visitor
- Identifies consistent patterns even when IP changes (VPN usage)

### 4. Location Inference
- Analyzes fastest ping times to determine closest ASNs
- Infers country/region based on ASN geographic distribution
- Calculates confidence score based on pattern consistency

## Key Features

### ✅ VPN-Resistant Location Detection
- Ping patterns remain consistent even when behind VPN
- Can correlate visits across different VPN IPs
- Identifies original location despite VPN masking

### ✅ Cross-Visit Correlation
- Matches ping patterns across multiple visits
- Tracks location changes over time
- Detects when user moves vs. uses different VPN endpoints

### ✅ CNSA 2.0 Compliant
- SHA-384 hashing for pattern signatures
- Secure pattern storage and correlation
- Post-quantum ready architecture

## ASN Targets

### North America
- **AS15169** (Google LLC) - Global distribution
- **AS32934** (Facebook) - US-East
- **AS8075** (Microsoft) - US-West
- **AS16509** (Amazon) - US-East
- **AS20057** (Akamai) - US-Central

### Europe
- **AS3320** (Deutsche Telekom) - Central Europe
- **AS3215** (Orange) - Western Europe
- **AS3352** (Telefonica) - Southern Europe
- **AS1299** (Telia) - Northern Europe
- **AS6830** (Liberty Global) - UK

### Asia Pacific
- **AS4766** (Korea Telecom) - Korea
- **AS2914** (NTT) - Japan
- **AS4134** (China Telecom) - China
- **AS7473** (Singtel) - Singapore
- **AS4826** (Telstra) - Australia

### CDN Endpoints
- **Cloudflare** (1.1.1.1, 1.0.0.1)
- **Fastly** (151.101.1.140)
- **CloudFront** (13.32.0.0)

## Data Model

### AsnPingTiming
Stores individual ping measurements:
```csharp
- VisitId: Links to visit record
- ASN: Autonomous System Number
- ASNName: ASN organization name
- ASNCountry: ISO country code
- ASNRegion: Region/state
- PingTarget: IP or hostname
- PingTime: Average latency (ms)
- MinPingTime, MaxPingTime: Latency range
- Jitter: Variance in ping times
- PingAttempts, SuccessfulPings: Measurement stats
```

### AsnPingCorrelation
Correlates patterns across visits:
```csharp
- VisitorId: Links to visitor
- PatternHash: SHA-384 hash of ping pattern
- PatternData: JSON with normalized ping times
- InferredCountry/Region/City: Location inference
- InferredLatitude/Longitude: Geographic coordinates
- LocationConfidence: 0.0 to 1.0
- PatternSimilarity: Similarity score across visits
- IsBehindVPN: VPN detection flag
- OriginalLocation: Inferred location despite VPN
```

## Usage

### Client-Side Collection
The `asn-ping-timing.js` script automatically:
1. Measures ping times to ASN targets after page load
2. Creates normalized ping pattern
3. Stores data in `window.asnPingTimingData`
4. Tracking.js includes it in data sent to server

### Server-Side Processing
`AsnPingTimingService`:
1. Stores ping timing measurements
2. Creates pattern hash (SHA-384)
3. Correlates patterns across visits
4. Infers location from ping patterns
5. Detects VPN usage and original location

## Correlation Algorithm

### Pattern Similarity Calculation
1. **ASN Matching**: Find common ASNs between patterns
2. **Normalized Time Comparison**: Compare relative ping times
3. **Deviation Analysis**: Calculate average deviation
4. **Similarity Score**: 
   - ASN Match Ratio (60% weight)
   - Deviation Score (40% weight)
   - Threshold: >0.7 considered same pattern

### Location Inference
1. **Fastest ASNs**: Identify ASNs with lowest ping times
2. **Geographic Analysis**: Determine most common country/region
3. **Confidence Calculation**: Based on consistency of fastest ASNs
4. **VPN Detection**: Compare inferred location with IP geolocation

## Benefits

### ✅ Accurate Location Detection
- More accurate than IP geolocation alone
- Works across VPNs and proxies
- Can detect location changes

### ✅ Privacy-Preserving
- No GPS/geolocation API required
- Passive measurement (no user interaction)
- Respects user privacy while providing insights

### ✅ Cross-Visit Tracking
- Correlates users across sessions
- Tracks location changes over time
- Identifies VPN usage patterns

## Limitations

### Network Conditions
- Affected by network congestion
- ISP routing can vary
- Mobile networks less reliable

### Measurement Accuracy
- Browser timing limitations (~1ms precision)
- Cross-origin restrictions
- Firewall/proxy interference

### Privacy Considerations
- Can infer location without explicit consent
- Should be used responsibly
- Consider GDPR/privacy regulations

## Implementation Status

- ✅ Database models created
- ✅ Client-side measurement script
- ✅ Server-side processing service
- ✅ Pattern correlation algorithm
- ✅ Location inference logic
- ✅ VPN detection integration
- ✅ Cross-visit correlation

## Future Enhancements

1. **More ASN Targets**: Expand to 50+ ASNs globally
2. **Traceroute Integration**: Measure hop count and path
3. **Machine Learning**: Improve location inference accuracy
4. **Real-time Updates**: Update patterns dynamically
5. **Historical Analysis**: Track location trends over time

---

**CNSA 2.0 Compliant | VPN-Resistant | Cross-Visit Correlation**

