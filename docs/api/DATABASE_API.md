# Database API Documentation

## Overview

All tracking data is now stored exclusively in the SQLite database. No file-based storage is used.

## Database Location

- **File**: `TrackingData/tracking.db`
- **Type**: SQLite 3
- **Persistence**: Docker volume `tracking-data`

## API Endpoints

### Base URL
```
http://localhost:9080/TrackingData
```

### Available Actions

#### 1. Get All Visitors
```bash
GET /TrackingData?action=visitors&page=1&pageSize=50
```

**Response:**
```json
{
  "totalVisitors": 10,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1,
  "visitors": [
    {
      "id": 1,
      "fingerprintHash": "base64_sha384_hash...",
      "firstSeen": "2025-11-22T18:00:00Z",
      "lastSeen": "2025-11-22T18:30:00Z",
      "visitCount": 3,
      "userAgent": "Mozilla/5.0...",
      "platform": "Linux",
      "language": "en-US"
    }
  ]
}
```

#### 2. Get Visitor Details
```bash
GET /TrackingData?action=visitor&visitorId=1
```

**Response:**
```json
{
  "visitor": {
    "id": 1,
    "fingerprintHash": "...",
    "firstSeen": "2025-11-22T18:00:00Z",
    "lastSeen": "2025-11-22T18:30:00Z",
    "visitCount": 3,
    "userAgent": "Mozilla/5.0...",
    "platform": "Linux",
    "language": "en-US"
  },
  "summary": {
    "isNewVisitor": false,
    "visitorId": 1,
    "totalVisits": 3,
    "recentVisits": [...],
    "vpnProxyHistory": {
      "totalDetections": 3,
      "vpnDetectedCount": 2,
      "uniqueIPs": 2,
      "mostCommonSuspicionLevel": "High"
    }
  },
  "visits": [...]
}
```

#### 3. Get All Visits
```bash
GET /TrackingData?action=visits&page=1&pageSize=50
```

**Response:**
```json
{
  "totalVisits": 25,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1,
  "visits": [
    {
      "id": 1,
      "visitorId": 1,
      "timestamp": "2025-11-22T18:00:00Z",
      "sessionId": "sess_...",
      "remoteIP": "185.159.157.45",
      "forwardedFor": "185.159.157.45, 192.168.1.1",
      "userAgent": "Mozilla/5.0...",
      "referer": null,
      "visitorFingerprint": "...",
      "visitorTotalVisits": 3,
      "vpnDetected": true,
      "suspicionLevel": "Very High"
    }
  ]
}
```

#### 4. Get Visit Details (Standardized Output)
```bash
GET /TrackingData?action=visit&visitId=1
```

**Response:** Full standardized tracking output (see TRACKING_SYSTEM_SUMMARY.md)

#### 5. Get VPN Detections
```bash
GET /TrackingData?action=vpn&page=1&pageSize=50
```

**Response:**
```json
{
  "totalDetections": 15,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1,
  "detections": [
    {
      "id": 1,
      "visitId": 1,
      "visitorId": 1,
      "detectedAt": "2025-11-22T18:00:00Z",
      "remoteIP": "185.159.157.45",
      "ipChain": ["185.159.157.45", "192.168.1.1", "172.20.0.1"],
      "suspicionLevel": "Very High",
      "indicatorCount": 5,
      "indicators": [
        "X-Forwarded-For header present (proxy chain detected)",
        "Via header present",
        "Multiple IPs in chain (3 hops)",
        "IP matches known VPN provider range",
        "IP appears to be from datacenter"
      ],
      "isKnownVPNProvider": true,
      "isDatacenterIP": true,
      "isTorExitNode": false,
      "ipType": "IPv4 Public"
    }
  ]
}
```

#### 6. Get Statistics
```bash
GET /TrackingData?action=stats
```

**Response:**
```json
{
  "overview": {
    "totalVisitors": 10,
    "totalVisits": 25,
    "vpnDetections": 15,
    "vpnDetectionRate": 60.0,
    "returningVisitors": 5,
    "returningVisitorRate": 50.0
  },
  "suspicionLevels": [
    { "level": "Very High", "count": 8 },
    { "level": "High", "count": 5 },
    { "level": "Medium", "count": 2 }
  ],
  "topVPNProviders": [
    { "ip": "185.159.157.45", "count": 5 },
    { "ip": "91.219.123.45", "count": 3 }
  ],
  "recentActivity": {
    "last24Hours": 25,
    "last7Days": 25,
    "last30Days": 25
  }
}
```

#### 7. Export All Data
```bash
GET /TrackingData?action=export
```

**Response:** Complete database export with all visitors, visits, and VPN detections

## Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `action` | string | required | Action to perform |
| `visitorId` | int | optional | Visitor ID for visitor details |
| `visitId` | int | optional | Visit ID for visit details |
| `page` | int | 1 | Page number for pagination |
| `pageSize` | int | 50 | Number of results per page |

## Database Queries

### Direct SQLite Access

```bash
# Access database in Docker container
docker exec -it ask2ask-app sqlite3 /app/TrackingData/tracking.db

# Example queries
SELECT COUNT(*) FROM Visitors;
SELECT COUNT(*) FROM Visits;
SELECT COUNT(*) FROM VPNProxyDetections WHERE IsLikelyVPNOrProxy = 1;

# Get visitor with most visits
SELECT * FROM Visitors ORDER BY VisitCount DESC LIMIT 1;

# Get recent VPN detections
SELECT * FROM VPNProxyDetections 
WHERE IsLikelyVPNOrProxy = 1 
ORDER BY DetectedAt DESC 
LIMIT 10;

# Get visitors using VPNs
SELECT DISTINCT v.* 
FROM Visitors v
JOIN Visits vi ON v.Id = vi.VisitorId
JOIN VPNProxyDetections vpn ON vi.Id = vpn.VisitId
WHERE vpn.IsLikelyVPNOrProxy = 1;
```

## Data Flow

1. **Visitor arrives** → Tracking scripts collect data
2. **Data submitted** → POST to `/Tracking`
3. **Processing**:
   - VPN/Proxy detection performed
   - Fingerprint hash generated (SHA-384)
   - Visitor identified or created
   - Visit record created
   - VPN detection record created
   - All data saved to SQLite database
4. **Response** → Standardized JSON with visitor info and VPN detection

## No File Storage

**Important:** The system no longer writes tracking data to JSON files. All data is stored exclusively in the SQLite database.

- ❌ No individual JSON files per visit
- ❌ No daily JSONL log files
- ✅ All data in SQLite database
- ✅ Query via API endpoints
- ✅ Export functionality available

## Backup & Export

### Backup Database
```bash
# Copy database from Docker volume
docker cp ask2ask-app:/app/TrackingData/tracking.db ./backup_tracking.db

# Or use Docker volume
docker run --rm -v ask2askuk_tracking-data:/data -v $(pwd):/backup alpine \
  cp /data/tracking.db /backup/tracking_backup.db
```

### Export Data via API
```bash
# Export all data as JSON
curl "http://localhost:9080/TrackingData?action=export" > full_export.json
```

## Performance Considerations

- **Indexes**: Created on frequently queried fields
  - `Visitors.FingerprintHash` (unique)
  - `Visits.VisitorId`
  - `Visits.Timestamp`
  - `VPNProxyDetections.VisitId`

- **Pagination**: All list endpoints support pagination
- **Eager Loading**: Related data loaded efficiently with `Include()`
- **JSON Storage**: Full tracking data stored as JSON for flexibility

## Security

- Database file permissions managed by Docker
- No direct SQL injection risk (using EF Core)
- API endpoints have no authentication (add if needed for production)
- Consider rate limiting for API endpoints
- Implement data retention policies

## Future Enhancements

1. **Authentication** - Protect API endpoints
2. **Rate Limiting** - Prevent abuse
3. **Data Retention** - Automatic cleanup of old data
4. **Caching** - Redis for frequently accessed data
5. **Analytics Dashboard** - Web UI for data visualization
6. **Real-time Updates** - WebSocket for live tracking
7. **Data Anonymization** - GDPR compliance features

