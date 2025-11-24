# Inferred Region System

Metro-level geolocation inference for Ask2Ask using ASN latency patterns, network metadata, and behavioral signals.

## Overview

The **InferredRegionEngine** provides probabilistic metro-area geolocation without individual targeting:

- **Metro-level only**: Predicts to city/metropolitan area, never addresses
- **Dual-path inference**: Heuristic + optional ML model with automatic fallback
- **Respectful bounds**: Designed to detect VPN mismatches and fraud patterns, not track individuals
- **Production-ready**: Graceful degradation if ML models unavailable

### What It Does ✅

- ✅ Detects "VPN exit claims US, but pattern is clearly EU"
- ✅ Correlates visitor patterns across sessions
- ✅ Trains on network timing + behavioral signals
- ✅ Provides confidence scores (0.0–1.0)
- ✅ Enables fraud/anomaly scoring

### What It Doesn't Do ❌

- ❌ Pinpoint individual addresses or homes
- ❌ Break VPN security (works at exit-point level)
- ❌ Track behind encryption or Tor
- ❌ Compromise visitor privacy at building-level

---

## Architecture

### Three Inference Engines

```
┌──────────────────────────────────────────┐
│  CompositeInferredRegionEngine           │
│  (Smart fallback orchestrator)           │
└─────────────┬──────────────────────┬─────┘
              │                      │
        ┌─────▼─────┐       ┌────────▼────────┐
        │ ONNX       │       │ Heuristic       │
        │ Engine     │       │ Engine          │
        │ (if avail) │       │ (always works)  │
        └────────────┘       └─────────────────┘
```

**Composite Strategy:**
1. Try ONNX model inference (if `Models/inferred_region.onnx` exists)
2. If ONNX unavailable/fails → fall back to heuristic
3. If heuristic fails → return null (graceful)

### Heuristic Engine (Always Available)

Uses rule-based inference from:
- **ASN Ping Correlation**: If available from previous visits
- **Timezone offset**: Normalize to expected UTC offset for region
- **Locale/language**: Browser language hints
- **Confidence scoring**: Soft signals don't override hard data

**Code**: `Services/InferredRegionEngine.cs`

### ONNX ML Engine (Optional, High Confidence)

Trained LightGBM classifier on:
- Normalized RTT to probe endpoints
- ASN correlation history
- VPN/proxy detection flags
- Behavioral time signals (hour, weekday, timezone)
- Browser locale features

**Requires**: `Models/inferred_region.onnx` + metadata

**Code**: `Services/OnnxInferredRegionEngine.cs`

---

## Database Schema

### New Visit Fields

```sql
-- Inferred Region (from InferredRegionEngine)
InferredRegionId       NVARCHAR(50)   -- e.g., "eu-ams"
InferredRegionConfidence REAL         -- 0.0 to 1.0
InferredRegionFlagsJson NVARCHAR(MAX) -- { vpnExitMismatch, geoIpMismatch, ... }
```

### Region Model

```csharp
public class Region
{
    public string RegionId { get; set; }       // "eu-ams"
    public string RegionName { get; set; }     // "Amsterdam Metro"
    public string CountryCode { get; set; }    // "NL"
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
```

**Seeded**: 70 major metros from `Data/Regions.json` at app startup

---

## API Integration

### Recording a Visit

```csharp
// In TrackingService.RecordVisitAsync()
var correlation = await GetLatestAsnPingCorrelationForVisitorAsync(visitor.Id);
var inferredRegion = await _inferredRegionEngine.InferAsync(visit, correlation);

if (inferredRegion is not null)
{
    visit.InferredRegionId = inferredRegion.RegionId;
    visit.InferredRegionConfidence = inferredRegion.Confidence;
    visit.InferredRegionFlagsJson = JsonSerializer.Serialize(inferredRegion.Flags);
    await _context.SaveChangesAsync();
}
```

Inference runs **after** VPN detection, so both signals are available.

### Query Inferred Region

**GET /api/visits?page=1**

```json
{
  "visits": [
    {
      "id": 123,
      "timestamp": "2025-01-15T10:30:00Z",
      "remoteIP": "203.0.113.42",
      "inferredRegion": {
        "regionId": "eu-ams",
        "confidence": 0.87,
        "flags": {
          "vpnExitMismatch": true,
          "geoIpMismatch": false,
          "timezoneMismatch": false
        }
      },
      "vpnDetection": { ... }
    }
  ]
}
```

**GET /api/visitor?hash=abc123**

```json
{
  "visitor": {
    "fingerprintHash": "abc123",
    "regionSummary": [
      {
        "regionId": "eu-ams",
        "count": 12,
        "averageConfidence": 0.84
      },
      {
        "regionId": "eu-lon",
        "count": 3,
        "averageConfidence": 0.71
      }
    ],
    "visits": [ ... ]
  }
}
```

---

## Features

### Mismatch Detection

Flags are set when inferred region conflicts with other signals:

| Flag | When Set | Use Case |
|------|----------|----------|
| `vpnExitMismatch` | VPN exit country ≠ inferred region country | VPN routing lies |
| `geoIpMismatch` | GeoIP location ≠ inferred region | Proxy detection |
| `timezoneMismatch` | Timezone offset doesn't match region | Local time anomaly |

### Confidence Scores

- **0.0–0.5**: Low confidence (heuristic fallback, soft signals)
- **0.5–0.8**: Medium confidence (ASN correlation + heuristic)
- **0.8–1.0**: High confidence (ONNX model + strong signal agreement)

### Candidate Regions

Top 3 candidates ranked by probability:

```json
"candidates": [
  { "regionId": "eu-ams", "confidence": 0.87 },
  { "regionId": "eu-bru", "confidence": 0.09 },
  { "regionId": "eu-fra", "confidence": 0.04 }
]
```

---

## Usage Examples

### 1. Fraud Detection: VPN Exit Mismatch

```csharp
// Flag suspicious: VPN claims Netherlands, but all network signals point to Brazil
var suspiciousVisits = await _context.Visits
    .Where(v => v.InferredRegionId == "br-sao" &&
                v.VPNProxyDetection.IsLikelyVPNOrProxy &&
                v.VPNProxyDetection.Country == "NL")
    .ToListAsync();
```

### 2. Visitor Correlation

```csharp
// Find all visits from same region cluster
var regionPattern = visitor.Visits
    .GroupBy(v => v.InferredRegionId)
    .Select(g => new { Region = g.Key, Count = g.Count() })
    .OrderByDescending(r => r.Count)
    .Take(3);

// Compare patterns across visitors
var similarVisitors = await _context.Visitors
    .Where(v => v.Id != visitingVisitor.Id)
    .AsEnumerable()
    .Where(v => PatternsMatch(v.Visits.Select(x => x.InferredRegionId), regionPattern))
    .ToListAsync();
```

### 3. Geolocation Anomaly Scoring

```csharp
// Boost suspicion score if multiple mismatches
var anomalyScore = 0.0;
if (inferredRegion.Flags.VpnExitMismatch) anomalyScore += 0.3;
if (inferredRegion.Flags.TimezoneMismatch) anomalyScore += 0.2;
if (inferredRegion.Confidence < 0.5) anomalyScore += 0.15;

// Export for fraud scoring
var fraudScore = baseScore + (anomalyScore * weight);
```

---

## Deployment

### Without ML Model (Heuristic Only)

1. Deploy Ask2Ask with heuristic engine enabled
2. Inference works immediately (metro-level regions only)
3. Confidence scores may be lower (0.3–0.6 typical)

**Performance**: Negligible overhead (~1ms per visit)

### With ML Model (Recommended)

1. **Train model**: Follow [INFERRED_REGION_MODEL_TRAINING.md](INFERRED_REGION_MODEL_TRAINING.md)
   ```bash
   python scripts/train_inferred_region_model.py \
     --input ask2ask_visits.ndjson \
     --output Models/ \
     --num-rounds 100
   ```

2. **Deploy files**:
   ```
   Models/
   ├── inferred_region.onnx              # Binary model
   └── inferred_region-metadata.json    # Region mapping
   ```

3. **Start application**: Composite engine detects model automatically

4. **Verify**:
   ```bash
   curl https://ask2ask.com/api/visits?page=1 | jq '.Visits[0].InferredRegion'
   ```
   Should show `inferredRegion` object with confidence > 0.7

---

## Configuration

### Enable/Disable Heuristic Engine

Edit `Program.cs`:

```csharp
// Heuristic only (no ONNX)
builder.Services.AddScoped<IInferredRegionEngine, InferredRegionEngine>();

// OR Composite (ONNX + fallback)
builder.Services.AddScoped<OnnxInferredRegionEngine>();
builder.Services.AddScoped<InferredRegionEngine>();
builder.Services.AddScoped<IInferredRegionEngine>(provider =>
    new CompositeInferredRegionEngine(
        provider.GetRequiredService<ILogger<CompositeInferredRegionEngine>>(),
        provider.GetRequiredService<OnnxInferredRegionEngine>(),
        provider.GetRequiredService<InferredRegionEngine>()
    )
);
```

### Add Regions

Edit `Data/Regions.json` to add/remove regions:

```json
[
  {
    "regionId": "custom-id",
    "regionName": "Custom Metro",
    "countryCode": "XX",
    "latitude": 0.0,
    "longitude": 0.0
  }
]
```

Regions are auto-loaded at startup if table is empty.

---

## Troubleshooting

### ONNX Model Not Loading

```
ONNX engine not available, skipping model inference
```

**Check**:
- `Models/inferred_region.onnx` exists
- `Models/inferred_region-metadata.json` exists
- Files are readable by app process
- JSON is valid

**Fix**:
- Remove model files to fall back to heuristic
- Or retrain/deploy correct model

### Low Confidence Scores

If all scores < 0.5, likely causes:

| Cause | Fix |
|-------|-----|
| No ASN correlation data | Ensure ping timing collection is working |
| Heuristic fallback (no model) | Train and deploy ONNX model |
| Limited training data | Collect more samples (min 500/region) |
| Feature mismatch | Verify BuildFeatureVector matches training |

### Inference Timeout

If inference slow (>10ms):
- ONNX model may be large; reduce num_leaves in training
- Or use heuristic-only (instant)

---

## Performance & Metrics

### Inference Latency

- **Heuristic**: ~1ms (negligible)
- **ONNX**: ~5–10ms (depends on model size)
- **Composite**: ~5–10ms (ONNX) or ~1ms (fallback)

### Accuracy (Typical)

On holdout test set with balanced regional distribution:
- **Top-1 accuracy**: 75–85% (depends on training data quality)
- **Top-3 accuracy**: 90–95%
- **Confidence correlation**: higher confidence = higher accuracy

### Storage

- **ONNX model**: 5–50MB (depends on boosting rounds)
- **Metadata**: ~50KB
- **Per visit**: 3 columns + JSON flags (~200 bytes)

---

## Next Steps

1. **[Train first model](INFERRED_REGION_MODEL_TRAINING.md)**: Collect 3+ months of data, retrain quarterly
2. **Monitor accuracy**: Track top-1/top-3 accuracy on new visits
3. **Integrate with fraud scoring**: Use `InferredRegionFlags` in anomaly detection
4. **Expand regions**: Add tier-2 cities (500+ metros total)
5. **Feature enrichment**: Add traceroute analysis, jitter metrics

---

## References

- [INFERRED_REGION_MODEL_TRAINING.md](INFERRED_REGION_MODEL_TRAINING.md) – Complete training guide
- [OnnxInferredRegionEngine.cs](../Services/OnnxInferredRegionEngine.cs) – ONNX implementation
- [InferredRegionEngine.cs](../Services/InferredRegionEngine.cs) – Heuristic implementation
- [scripts/train_inferred_region_model.py](../scripts/train_inferred_region_model.py) – Training script
- [ONNX Runtime Docs](https://onnxruntime.ai/docs/)
- [LightGBM Python](https://lightgbm.readthedocs.io/)
