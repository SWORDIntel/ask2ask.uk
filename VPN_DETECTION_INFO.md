# VPN/Proxy Detection System

## Overview

The tracking system now includes comprehensive VPN and proxy detection capabilities to identify when visitors are using anonymization services.

## Detection Methods

### 1. **Proxy Header Analysis**
Detects common proxy headers that reveal proxy/VPN usage:
- `X-Forwarded-For` - Shows IP chain through proxies
- `X-Real-IP` - Original client IP
- `Via` - Proxy server information
- `Proxy-Connection` - Proxy connection header
- `CF-Connecting-IP` - Cloudflare proxy
- `True-Client-IP` - Akamai/Cloudflare
- And many more...

### 2. **IP Chain Analysis**
Tracks the complete chain of IP addresses from client to server:
- Counts number of hops
- Identifies intermediate proxies
- Detects IP address changes

### 3. **Known VPN Provider Detection**
Checks against known VPN provider IP ranges:
- **NordVPN**: `185.159.*`
- **ExpressVPN**: `91.219.*`
- **ProtonVPN**: `89.238.*`
- **Private Internet Access**: `104.244.*`
- **Mullvad**: `193.32.*`

### 4. **Datacenter IP Detection**
Identifies IPs from major cloud/datacenter providers (common for VPNs):
- **AWS**: `3.*, 13.*, 18.*, 34.*, 35.*, 52.*, 54.*`
- **Google Cloud**: `104.196.*, 104.197.*, 104.198.*`
- **Azure**: `13.64.*, 13.65.*, 13.66.*, 40.*, 52.*`
- **DigitalOcean**: `167.172.*, 157.230.*, 159.89.*, 147.182.*, 143.198.*`

### 5. **Tor Exit Node Detection**
Placeholder for Tor exit node detection (requires external list)

### 6. **IP Geolocation**
Analyzes IP address characteristics:
- Private IP detection (10.*, 172.16-31.*, 192.168.*)
- Localhost detection
- IPv4 vs IPv6 classification

## Suspicion Levels

The system calculates a suspicion level based on indicators:

- **Very High**: 5+ indicators
- **High**: 3-4 indicators
- **Medium**: 2 indicators
- **Low**: 1 indicator
- **None**: 0 indicators

## Data Collected

For each visitor, the system records:

```json
{
  "VPNProxyDetection": {
    "RemoteIP": "185.159.157.45",
    "IPChain": ["185.159.157.45", "192.168.1.1", "172.20.0.1"],
    "ProxyHeaders": {
      "X-Forwarded-For": "185.159.157.45, 192.168.1.1",
      "Via": "1.1 proxy.example.com"
    },
    "DetectionIndicators": [
      "X-Forwarded-For header present (proxy chain detected)",
      "Via header present: 1.1 proxy.example.com (proxy detected)",
      "Multiple IPs in chain (3 hops)",
      "IP matches known VPN provider range",
      "IP appears to be from datacenter (common for VPNs/proxies)"
    ],
    "SuspicionLevel": "Very High",
    "IsLikelyVPNOrProxy": true,
    "Analysis": {
      "HasProxyHeaders": true,
      "IPHopCount": 3,
      "HasViaHeader": true,
      "HasForwardedFor": true,
      "IndicatorCount": 5
    }
  },
  "IPGeolocation": {
    "IP": "185.159.157.45",
    "Note": "Geolocation requires external API service",
    "RecommendedServices": [
      "MaxMind GeoIP2",
      "IP2Location",
      "ipapi.co",
      "ipinfo.io",
      "ipgeolocation.io"
    ],
    "LocalAnalysis": {
      "IsPrivateIP": false,
      "IsLocalhost": false,
      "IPType": "IPv4 Public"
    }
  }
}
```

## Testing VPN Detection

### Test with cURL (simulating VPN/Proxy):

```bash
curl -X POST http://localhost:9080/Tracking \
  -H "Content-Type: application/json" \
  -H "X-Forwarded-For: 185.159.157.45, 192.168.1.1" \
  -H "Via: 1.1 proxy.example.com" \
  -H "Proxy-Connection: keep-alive" \
  -d '{"test": "vpn detection"}'
```

### Expected Detection:
- ✅ X-Forwarded-For header detected
- ✅ Via header detected  
- ✅ Multiple IP hops
- ✅ Known VPN provider IP (NordVPN range)
- ✅ Suspicion Level: Very High

## Production Enhancements

For production use, consider integrating:

1. **VPN Detection APIs**:
   - IPHub.info
   - GetIPIntel.net
   - IPQS (IP Quality Score)
   - VPNBlocker

2. **Tor Exit Node Lists**:
   - https://check.torproject.org/exit-addresses
   - Update daily

3. **Geolocation Services**:
   - MaxMind GeoIP2 (most accurate)
   - IP2Location
   - ipapi.co

4. **ASN (Autonomous System Number) Lookup**:
   - Identify hosting providers
   - Detect VPN/proxy ASNs

## Privacy & Legal Considerations

⚠️ **Important**: This tracking system is for educational/research purposes.

- Always disclose data collection practices
- Obtain user consent where required
- Comply with GDPR, CCPA, and local privacy laws
- Consider ethical implications of VPN/proxy detection
- Users have legitimate reasons to use VPNs (privacy, security, geo-restrictions)

## Purpose

This system demonstrates:
- How websites can detect anonymization attempts
- The difficulty of maintaining online privacy
- The importance of transparency in data collection
- Educational value in understanding tracking techniques

