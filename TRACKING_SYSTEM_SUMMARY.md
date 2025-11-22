# Comprehensive Tracking System with VPN Detection & Database

## System Overview

The tracking system now includes:
1. **VPN/Proxy Detection** - Identifies anonymization attempts
2. **SQLite Database** - Persistent storage with visitor correlation
3. **Standardized Output Format** - Consistent data structure
4. **Cross-Visit Correlation** - Track returning visitors
5. **CNSA 2.0 Compliance** - SHA-384 hashing for data integrity

## Database Schema

### Tables

#### 1. **Visitors** Table
Tracks unique visitors across multiple visits using fingerprint hashing.

```sql
- Id (Primary Key)
- FingerprintHash (Unique Index) - SHA-384 hash for identification
- FirstSeen - First visit timestamp
- LastSeen - Most recent visit timestamp
- VisitCount - Total number of visits
- UserAgent - Browser user agent
- Platform - Operating system
- Language - Browser language
```

#### 2. **Visits** Table
Records each individual visit with full tracking data.

```sql
- Id (Primary Key)
- VisitorId (Foreign Key -> Visitors)
- Timestamp - Visit timestamp
- SessionId - Session identifier
- RemoteIP - Visitor's IP address
- ForwardedFor - X-Forwarded-For header
- RealIP - X-Real-IP header
- UserAgent - Full user agent string
- Referer - HTTP referer
- BrowserFingerprint - Canvas/WebGL/Audio fingerprints
- HardwareConcurrency - CPU cores
- ScreenResolution - Display resolution
- TrackingDataJson - Full JSON data
- SHA384Hash - CNSA 2.0 compliant hash
```

#### 3. **VPNProxyDetections** Table
Stores VPN/Proxy detection results for each visit.

```sql
- Id (Primary Key)
- VisitId (Foreign Key -> Visits)
- RemoteIP - Detected IP address
- IPChain - JSON array of IP hops
- ProxyHeaders - JSON object of proxy headers
- DetectionIndicators - JSON array of indicators
- SuspicionLevel - None/Low/Medium/High/Very High
- IsLikelyVPNOrProxy - Boolean detection result
- IsKnownVPNProvider - Matches known VPN IP range
- IsDatacenterIP - Datacenter-hosted IP
- IsTorExitNode - Tor exit node detection
- IPType - IPv4/IPv6/Private/Localhost
- DetectedAt - Detection timestamp
```

## Standardized Output Format

Every visit produces a standardized JSON output:

```json
{
  "VisitId": 1,
  "VisitorId": 1,
  "Timestamp": "2025-11-22T18:00:00Z",
  "SessionId": "sess_1732298400_abc123",
  
  "Identity": {
    "FingerprintHash": "base64_sha384_hash...",
    "IsReturningVisitor": false,
    "TotalVisits": 1,
    "FirstSeen": "2025-11-22T18:00:00Z",
    "LastSeen": "2025-11-22T18:00:00Z"
  },
  
  "Network": {
    "RemoteIP": "185.159.157.45",
    "ForwardedFor": "185.159.157.45, 192.168.1.1",
    "IPChain": ["185.159.157.45", "192.168.1.1", "172.20.0.1"]
  },
  
  "VPNProxy": {
    "IsDetected": true,
    "SuspicionLevel": "Very High",
    "IndicatorCount": 5,
    "Indicators": [
      "X-Forwarded-For header present (proxy chain detected)",
      "Via header present: 1.1 proxy.example.com",
      "Multiple IPs in chain (3 hops)",
      "IP matches known VPN provider range",
      "IP appears to be from datacenter"
    ],
    "IsKnownVPNProvider": true,
    "IsDatacenterIP": true,
    "IsTorExitNode": false,
    "IPType": "IPv4 Public"
  },
  
  "Browser": {
    "UserAgent": "Mozilla/5.0...",
    "Platform": "Linux",
    "Language": "en-US"
  },
  
  "Security": {
    "SHA384Hash": "base64_hash...",
    "CNSA2_0_Compliant": true
  },
  
  "RawData": { /* Full tracking JSON */ }
}
```

## Visitor Correlation

The system tracks visitors across multiple visits:

### Visitor Summary
```json
{
  "IsNewVisitor": false,
  "VisitorId": 1,
  "FingerprintHash": "...",
  "FirstSeen": "2025-11-22T10:00:00Z",
  "LastSeen": "2025-11-22T18:00:00Z",
  "TotalVisits": 5,
  
  "RecentVisits": [
    {
      "VisitId": 5,
      "Timestamp": "2025-11-22T18:00:00Z",
      "RemoteIP": "185.159.157.45",
      "IsLikelyVPNOrProxy": true,
      "SuspicionLevel": "Very High"
    },
    // ... previous visits
  ],
  
  "VPNProxyHistory": {
    "TotalDetections": 5,
    "VPNDetectedCount": 3,
    "UniqueIPs": 2,
    "MostCommonSuspicionLevel": "High"
  }
}
```

## API Response

When tracking data is submitted, the response includes:

```json
{
  "success": true,
  "sessionId": "sess_1732298400_abc123",
  "hash": "SHA384_hash...",
  "timestamp": "2025-11-22T18:00:00Z",
  "message": "Data collected successfully",
  
  "cnsa2_0": {
    "compliant": true,
    "algorithms": ["SHA-384", "ML-KEM-1024 (pending)", "ML-DSA-87 (pending)"]
  },
  
  "visitor": {
    "id": 1,
    "isNew": false,
    "totalVisits": 5,
    "firstSeen": "2025-11-22T10:00:00Z",
    "lastSeen": "2025-11-22T18:00:00Z"
  },
  
  "vpnProxy": {
    "IsDetected": true,
    "SuspicionLevel": "Very High",
    "IndicatorCount": 5,
    "Indicators": [...],
    "IsKnownVPNProvider": true,
    "IsDatacenterIP": true,
    "IsTorExitNode": false,
    "IPType": "IPv4 Public"
  },
  
  "standardizedOutput": { /* Full standardized format */ }
}
```

## Data Storage

### Database Location
- **Path**: `TrackingData/tracking.db`
- **Type**: SQLite
- **Persistence**: Docker volume `tracking-data`

### File Storage (Legacy)
- **Individual JSON files**: `TrackingData/YYYYMMDD_HHmmss_sessionId.json`
- **Daily logs**: `TrackingData/daily_YYYYMMDD.jsonl`

## VPN Detection Methods

1. **Proxy Header Analysis** - X-Forwarded-For, Via, Proxy-Connection, etc.
2. **IP Chain Analysis** - Multiple hops indicate proxies
3. **Known VPN Providers** - NordVPN, ExpressVPN, ProtonVPN, etc.
4. **Datacenter IPs** - AWS, Google Cloud, Azure, DigitalOcean
5. **Tor Exit Nodes** - Placeholder for Tor detection
6. **IP Geolocation** - Private/Public IP classification

## Suspicion Levels

| Level | Score | Indicators |
|-------|-------|------------|
| None | 0 | No indicators |
| Low | 1 | 1 indicator |
| Medium | 2 | 2 indicators |
| High | 3-4 | 3-4 indicators |
| Very High | 5+ | 5+ indicators |

## Query Examples

### Find all VPN users
```csharp
var vpnUsers = await _context.VPNProxyDetections
    .Where(v => v.IsLikelyVPNOrProxy)
    .Include(v => v.Visit)
        .ThenInclude(visit => visit.Visitor)
    .ToListAsync();
```

### Get visitor history
```csharp
var visitor = await _context.Visitors
    .Include(v => v.Visits)
        .ThenInclude(visit => visit.VPNProxyDetection)
    .FirstOrDefaultAsync(v => v.FingerprintHash == hash);
```

### Find high-risk visits
```csharp
var highRisk = await _context.VPNProxyDetections
    .Where(v => v.SuspicionLevel == "Very High" || v.SuspicionLevel == "High")
    .Include(v => v.Visit)
    .ToListAsync();
```

## Security & Privacy

⚠️ **Important Considerations:**

1. **Data Retention** - Implement data retention policies
2. **GDPR Compliance** - Provide data deletion mechanisms
3. **User Consent** - Always obtain informed consent
4. **Purpose Limitation** - Use data only for stated purposes
5. **Data Minimization** - Collect only necessary data
6. **Security** - Protect stored data with encryption
7. **Transparency** - Disclose all tracking practices

## Production Recommendations

1. **Add Geolocation API** - MaxMind GeoIP2, IP2Location
2. **VPN Detection API** - IPHub, GetIPIntel, IPQS
3. **Tor Exit Node List** - Daily updates from torproject.org
4. **Database Backups** - Regular automated backups
5. **Data Anonymization** - Hash/encrypt sensitive data
6. **Access Controls** - Restrict database access
7. **Audit Logging** - Log all data access
8. **HTTPS Only** - Enforce encrypted connections

## Testing

### Test VPN Detection
```bash
# Simulate VPN/Proxy
curl -X POST http://localhost:9080/Tracking \
  -H "Content-Type: application/json" \
  -H "X-Forwarded-For: 185.159.157.45, 192.168.1.1" \
  -H "Via: 1.1 proxy.example.com" \
  -d '{"test": "vpn detection"}'
```

### Check Database
```bash
# Access SQLite database
docker exec -it ask2ask-app sqlite3 /app/TrackingData/tracking.db

# Query visitors
SELECT * FROM Visitors;

# Query VPN detections
SELECT * FROM VPNProxyDetections WHERE IsLikelyVPNOrProxy = 1;
```

## Files Modified/Created

- ✅ `Ask2Ask.csproj` - Added Entity Framework Core packages
- ✅ `Program.cs` - Registered database and services
- ✅ `Data/TrackingDbContext.cs` - Database context and models
- ✅ `Services/TrackingService.cs` - Business logic service
- ✅ `Pages/Tracking.cshtml.cs` - Updated to use database
- ✅ `VPN_DETECTION_INFO.md` - VPN detection documentation
- ✅ `TRACKING_SYSTEM_SUMMARY.md` - This file

## Deployment

The system is deployed with Docker + Caddy:
- **URL**: http://localhost:9080/Index (development)
- **Production**: https://ask2ask.uk (with SSL)
- **Database**: Persisted in Docker volume
- **Logs**: Console + daily JSONL files

