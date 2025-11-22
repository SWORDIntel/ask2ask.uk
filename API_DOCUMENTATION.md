# Ask2Ask API Documentation
## CNSA 2.0 Compliant Secure API Endpoints

### Overview

The Ask2Ask API provides secure access to tracking data with CNSA 2.0 compliant Zero Knowledge Proof (ZKP) authentication, rate limiting, and network isolation for high-security endpoints.

---

## Security Features

### ✅ CNSA 2.0 Compliance
- **SHA-384** hashing for API keys and data integrity
- **TLS 1.3** with strong cipher suites (AES-256-GCM, ChaCha20-Poly1305)
- **ECDSA P-384** signatures for Zero Knowledge Proof authentication
- **Post-quantum ready** architecture

### ✅ Authentication
- **API Key Authentication**: All endpoints require `X-API-Key` header
- **Zero Knowledge Proof (ZKP)**: ECDSA P-384 signatures for export/admin endpoints
- **Scope-based Authorization**: read, export, admin scopes
- **Request Signing**: HMAC-SHA384 for request integrity
- **Replay Protection**: Timestamp + nonce validation
- **Rate Limiting**: 100 requests/minute (general), 10 requests/hour (export)

### ✅ Network Isolation
- **Public Network** (172.20.0.0/16): Web traffic
- **Telemetry Network** (10.10.0.0/24): API/monitoring access only
- Production: Telemetry network is fully isolated (internal: true)

---

## Network Architecture

### Production Setup

```
Internet
  │
  ├─> ask2ask.uk (Public)
  │   └─> Caddy (172.20.0.20) ──> ASP.NET App (172.20.0.10)
  │
  └─> api.ask2ask.uk (Telemetry Network Only)
      └─> Caddy (10.10.0.20) ──[ZKP]──> ASP.NET App (10.10.0.10)
```

### Local Testing

```
localhost:9080 (HTTP)
  └─> All endpoints accessible for testing
```

---

## API Endpoints

### Base URLs

- **Production**: `https://api.ask2ask.uk`
- **Local Testing**: `http://localhost:9080`

---

## 1. Statistics Endpoint

Get aggregated tracking statistics.

### Request

```http
GET /api/stats
X-API-Key: <your-read-api-key>
```

### Required Scope
- `read`

### Response

```json
{
  "success": true,
  "timestamp": "2025-11-22T19:19:43.067Z",
  "data": {
    "overview": {
      "totalVisitors": 1234,
      "totalVisits": 5678,
      "vpnDetections": 234,
      "vpnDetectionRate": 0.0412,
      "returningVisitors": 456,
      "returningVisitorRate": 0.3696
    },
    "suspicionLevels": [
      { "level": "High", "count": 45 },
      { "level": "Medium", "count": 123 },
      { "level": "Low", "count": 66 }
    ],
    "recentActivity": {
      "last24Hours": 234,
      "last7Days": 1234,
      "last30Days": 4567
    }
  }
}
```

### Example

```bash
curl "https://api.ask2ask.uk/api/stats" \
  -H "X-API-Key: your-read-api-key"
```

---

## 2. Visits Endpoint

Get paginated list of all visits.

### Request

```http
GET /api/visits?page=1&pageSize=50
X-API-Key: <your-read-api-key>
```

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number (1-indexed) |
| `pageSize` | integer | 50 | Items per page (max: 1000) |

### Required Scope
- `read`

### Response

```json
{
  "success": true,
  "timestamp": "2025-11-22T19:19:48.163Z",
  "data": {
    "totalVisits": 5678,
    "page": 1,
    "pageSize": 50,
    "totalPages": 114,
    "visits": [
      {
        "id": 1234,
        "visitorId": 567,
        "visitorHash": "abc123...",
        "timestamp": "2025-11-22T18:30:00Z",
        "remoteIP": "1.2.3.4",
        "userAgent": "Mozilla/5.0...",
        "sha384Hash": "def456...",
        "vpnDetection": {
          "remoteIP": "1.2.3.4",
          "suspicionLevel": "High",
          "isLikelyVPNOrProxy": true
        }
      }
    ]
  }
}
```

### Example

```bash
curl "https://api.ask2ask.uk/api/visits?page=1&pageSize=100" \
  -H "X-API-Key: your-read-api-key"
```

---

## 3. Visitor Details Endpoint

Get detailed information about a specific visitor including all their visits.

### Request

```http
GET /api/visitor?hash=<fingerprint-hash>
X-API-Key: <your-read-api-key>
```

### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `hash` | string | Yes | Visitor fingerprint hash (SHA-384) |

### Required Scope
- `read`

### Response

```json
{
  "success": true,
  "timestamp": "2025-11-22T19:20:00Z",
  "data": {
    "id": 567,
    "fingerprintHash": "abc123...",
    "firstSeen": "2025-11-01T10:00:00Z",
    "lastSeen": "2025-11-22T18:30:00Z",
    "visitCount": 15,
    "userAgent": "Mozilla/5.0...",
    "platform": "Linux x86_64",
    "language": "en-US",
    "visits": [
      {
        "id": 1234,
        "timestamp": "2025-11-22T18:30:00Z",
        "remoteIP": "1.2.3.4",
        "userAgent": "Mozilla/5.0...",
        "sha384Hash": "def456...",
        "vpnDetection": {
          "remoteIP": "1.2.3.4",
          "ipChain": "[\"1.2.3.4\", \"5.6.7.8\"]",
          "suspicionLevel": "High",
          "isLikelyVPNOrProxy": true,
          "detectionIndicators": "[\"Multiple IP hops\", \"Known VPN provider\"]"
        }
      }
    ]
  }
}
```

### Example

```bash
curl "https://api.ask2ask.uk/api/visitor?hash=abc123..." \
  -H "X-API-Key: your-read-api-key"
```

---

## 4. Export Endpoint (Elasticsearch Integration)

Export entire database for integration with Elasticsearch or other analytics systems.

### Request

```http
GET /api/export?format=ndjson&since=2025-01-01&limit=1000
X-API-Key: <your-export-api-key>
```

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `format` | string | ndjson | Export format: `ndjson`, `json`, `bulk` |
| `since` | datetime | null | Only export visits after this date (ISO 8601) |
| `limit` | integer | null | Limit number of records |

### Required Scope
- `export`

### Required Authentication (Production)
- **API Key** with `export` scope
- **ZKP Signature** (ECDSA P-384) - See `docs/ZKP_CLIENT_EXAMPLES.md`

### Formats

#### 1. NDJSON (Newline-Delimited JSON)
Streaming format, one JSON object per line. Ideal for large datasets.

```bash
curl "https://api.ask2ask.uk/api/export?format=ndjson" \
  -H "X-API-Key: your-export-api-key" \
  --cert client-cert.pem \
  --key client-key.pem \
  --cacert ca.crt
```

#### 2. JSON
Standard JSON array. Good for small datasets.

```bash
curl "https://api.ask2ask.uk/api/export?format=json&limit=100" \
  -H "X-API-Key: your-export-api-key" \
  --cert client-cert.pem \
  --key client-key.pem \
  --cacert ca.crt
```

#### 3. Bulk (Elasticsearch Bulk API)
Ready for direct import into Elasticsearch.

```bash
curl "https://api.ask2ask.uk/api/export?format=bulk" \
  -H "X-API-Key: your-export-api-key" \
  --cert client-cert.pem \
  --key client-key.pem \
  --cacert ca.crt | \
curl -X POST "https://elasticsearch:9200/_bulk" \
  -H "Content-Type: application/x-ndjson" \
  --data-binary @-
```

### Response Structure

Each record includes:

```json
{
  "exportTimestamp": "2025-11-22T19:20:00Z",
  "recordType": "visit",
  "dataHash": "sha384-hash-for-integrity",
  "visitor": {
    "id": 567,
    "fingerprintHash": "abc123...",
    "firstSeen": "2025-11-01T10:00:00Z",
    "lastSeen": "2025-11-22T18:30:00Z",
    "visitCount": 15
  },
  "visit": {
    "id": 1234,
    "timestamp": "2025-11-22T18:30:00Z",
    "sessionId": "sess_123...",
    "network": {
      "remoteIP": "1.2.3.4",
      "httpVersion": "h2",
      "http2Support": true
    },
    "fingerprints": {
      "canvas": "hash1",
      "webgl": "hash2",
      "audio": "hash3",
      "cpu": "hash4",
      "webgpu": "hash5"
    },
    "hardware": {
      "hardwareConcurrency": 8,
      "screenResolution": "1920x1080",
      "batteryLevel": 0.85
    },
    "fullTrackingData": "{ ... all 130+ fields ... }"
  },
  "vpnProxy": {
    "isLikelyVPNOrProxy": true,
    "suspicionLevel": "High",
    "detectionIndicators": ["Multiple IP hops", "Known VPN provider"]
  }
}
```

---

## Authentication Setup

### 1. Generate API Keys

```bash
cd /home/john/Documents/ask2ask.uk
bash scripts/generate-api-keys.sh
```

This generates three CNSA 2.0 compliant 512-bit API keys:
- **Read Key**: For stats, visits, visitor endpoints
- **Export Key**: For database export
- **Admin Key**: For all endpoints

### 2. Update Configuration

Edit `appsettings.Api.json`:

```json
{
  "ApiKeys": [
    {
      "Key": "your-generated-read-key",
      "Scopes": ["read"],
      "Description": "Read-only access"
    },
    {
      "Key": "your-generated-export-key",
      "Scopes": ["read", "export"],
      "Description": "Export access for Elasticsearch"
    },
    {
      "Key": "your-generated-admin-key",
      "Scopes": ["*"],
      "Description": "Full admin access"
    }
  ]
}
```

### 3. Generate ZKP Key Pairs (Production Only)

For ZKP authentication on export endpoints:

```bash
# Generate ECDSA P-384 key pair
cd /opt/ask2ask.uk
bash scripts/generate-zkp-keypair.sh
```

This generates:
- `private-key.pem` - Keep secret, use for signing requests
- `public-key.pem` - Add to `appsettings.Api.json`

Add the public key to `appsettings.Api.json`:

```json
{
  "ApiKeys": [
    {
      "Key": "your-api-key",
      "PublicKey": "base64-encoded-public-key-from-script",
      "Scopes": ["read", "export"],
      "Description": "Export access with ZKP"
    }
  ]
}
```

See `docs/ZKP_CLIENT_EXAMPLES.md` for client implementation examples.

---

## Rate Limiting

### General Endpoints
- **100 requests per minute** per API key
- Applies to: `/api/stats`, `/api/visits`, `/api/visitor`

### Export Endpoints
- **10 requests per hour** per API key
- Applies to: `/api/export`

### Rate Limit Response

```json
{
  "error": "Rate limit exceeded",
  "message": "Too many requests. Please try again later."
}
```

---

## Error Responses

### 401 Unauthorized

```json
{
  "error": "API key required",
  "message": "Include X-API-Key header"
}
```

### 403 Forbidden

```json
{
  "error": "Invalid API key or insufficient permissions"
}
```

### 404 Not Found

```json
{
  "success": false,
  "error": "Visitor not found",
  "timestamp": "2025-11-22T19:20:00Z"
}
```

### 429 Too Many Requests

```json
{
  "error": "Rate limit exceeded",
  "message": "Too many requests. Please try again later."
}
```

### 500 Internal Server Error

```json
{
  "success": false,
  "error": "Failed to retrieve data",
  "timestamp": "2025-11-22T19:20:00Z"
}
```

---

## Production Deployment

### 1. Update docker-compose.yml

```yaml
caddy:
  volumes:
    - ./Caddyfile.production:/etc/caddy/Caddyfile:ro  # Use production Caddyfile
    - ./certs/ca.crt:/etc/caddy/certs/ca.crt:ro       # Mount CA cert
  networks:
    telemetry-network:
      internal: true  # Fully isolate telemetry network
```

### 2. Configure DNS

```
ask2ask.uk        A    your-server-ip
api.ask2ask.uk    A    your-server-ip
```

### 3. Deploy

```bash
docker-compose down
docker-compose up -d --build
```

### 4. Verify

```bash
# Test public site
curl https://ask2ask.uk

# Test API (from telemetry network only)
curl https://api.ask2ask.uk/api/stats \
  -H "X-API-Key: your-api-key"
```

---

## Elasticsearch Integration Example

### 1. Export data

```bash
curl "https://api.ask2ask.uk/api/export?format=bulk" \
  -H "X-API-Key: your-export-key" \
  --cert client-cert.pem \
  --key client-key.pem \
  --cacert ca.crt \
  -o tracking-data.ndjson
```

### 2. Import to Elasticsearch

```bash
curl -X POST "https://elasticsearch:9200/_bulk" \
  -H "Content-Type: application/x-ndjson" \
  --data-binary @tracking-data.ndjson
```

### 3. Create Kibana Dashboard

The exported data includes all 130+ tracking fields, ready for visualization in Kibana.

---

## Security Best Practices

1. **Never commit API keys** to version control
2. **Rotate API keys** regularly (every 90 days)
3. **Use mTLS** for all production export endpoints
4. **Monitor API usage** via logs
5. **Restrict telemetry network** access to trusted IPs only
6. **Enable Caddy access logs** for audit trail
7. **Use strong client certificates** (RSA 4096-bit, SHA-384)
8. **Keep CA private key** offline and encrypted

---

## Support

For issues or questions:
- Check logs: `docker logs ask2ask-app`
- Review Caddy logs: `docker logs ask2ask-caddy`
- Verify network: `docker network inspect ask2askuk_telemetry-network`

---

**CNSA 2.0 Compliant | Secure by Design | Production Ready**

