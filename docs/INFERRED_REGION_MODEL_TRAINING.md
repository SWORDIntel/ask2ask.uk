# Inferred Region Engine: Model Training & Deployment

This document explains how to train and deploy ML models for the `InferredRegionEngine` in Ask2Ask.

## Overview

The InferredRegionEngine supports two inference paths:

1. **Heuristic Fallback** (`InferredRegionEngine.cs`): Always available, rule-based classification using timezone, locale, ASN correlation data
2. **ONNX ML Model** (`OnnxInferredRegionEngine.cs`): High-confidence classification from trained LightGBM/XGBoost model
3. **Composite** (`CompositeInferredRegionEngine.cs`): Tries ONNX first, falls back to heuristic if model unavailable

The heuristic engine always works. The ONNX engine improves accuracy when a trained model is deployed.

---

## Training Pipeline

### 1. Export Training Data

Use Ask2Ask's existing export API to collect labeled training data:

```bash
# Authenticate and export visits with ASN correlations
curl -H "X-Api-Key: <your-api-key>" \
     "https://ask2ask.com/api/export?format=ndjson&since=2025-01-01" \
     > ask2ask_visits.ndjson
```

The export should include:
- Visit metadata (IP, VPN status, timezone, etc.)
- ASN ping correlation data (timing fingerprints)
- VPNProxyDetection results
- Timestamp and geolocation data (if available)

### 2. Label Data (Ground Truth)

For each visit, establish ground truth region:

```python
# Pseudocode: labeling strategy
for visit in visits:
    if visit.user_gps_location:
        region = find_nearest_region(visit.latitude, visit.longitude)
        label = region.region_id
    elif visit.asn_correlation.inferred_city:
        # Use ASN inference as fallback label
        region = find_region_by_city(visit.asn_correlation.inferred_city)
        label = region.region_id
    else:
        # Skip ambiguous samples
        skip_visit()
```

**Quality control**:
- Only use high-confidence labels
- Discard mobile network samples (high jitter, unpredictable)
- Require minimum 5 visits per user for consistency
- Flag samples with VPN/proxy discrepancies separately

### 3. Feature Engineering

Transform raw visits into feature vectors matching `OnnxInferredRegionEngine.BuildFeatureVector()`:

```python
import pandas as pd
import numpy as np

def build_features(visit, correlation):
    """
    Must match exact order in BuildFeatureVector():
    [rtt_avg, pattern_sim, matching_asns, has_history, tz_hours, is_vpn, suspicion, hour, weekday, locale]
    """
    features = []

    # Network features (normalized)
    if correlation:
        features.append(normalize_rtt(correlation['average_deviation']))
        features.append(correlation['pattern_similarity'])
        features.append(correlation['matching_asns'] / 50.0)
        features.append(1.0 if correlation['visit_count'] > 0 else 0.0)
    else:
        features.extend([0.0, 0.0, 0.0, 0.0])

    # Timezone (normalize to hours in [-1, 1])
    tz_hours = visit['timezone_offset_minutes'] / 60.0 if visit['timezone_offset_minutes'] else 0.0
    tz_normalized = max(-1.0, min(1.0, tz_hours / 12.0))
    features.append(tz_normalized)

    # VPN features
    features.append(1.0 if visit['is_vpn'] else 0.0)
    suspicion = {'high': 1.0, 'medium': 0.5, 'low': 0.25}.get(visit['suspicion'], 0.0)
    features.append(suspicion)

    # Time features
    hour = visit['timestamp'].hour
    features.append(hour / 24.0)
    weekday = visit['timestamp'].weekday()
    features.append(weekday / 7.0)

    # Locale feature
    locale_score = 0.5 if visit['locale'] and len(visit['locale']) > 2 else 0.0
    features.append(locale_score)

    return np.array(features)

# Build dataset
features = []
labels = []
for visit in labeled_visits:
    feat = build_features(visit, visit.correlation)
    features.append(feat)
    labels.append(visit.region_id)

X = np.array(features)
y = pd.Categorical(labels).codes  # Convert region IDs to class indices
```

### 4. Train LightGBM Model

```python
import lightgbm as lgb
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import LabelEncoder

# Prepare data
le = LabelEncoder()
y_encoded = le.fit_transform(y)  # region_id -> class index

X_train, X_test, y_train, y_test = train_test_split(
    X, y_encoded, test_size=0.2, random_state=42, stratify=y_encoded
)

# Build LightGBM classifier
params = {
    'objective': 'multiclass',
    'num_class': len(le.classes_),
    'boosting_type': 'gbdt',
    'num_leaves': 31,
    'learning_rate': 0.05,
    'feature_fraction': 0.8,
    'bagging_fraction': 0.8,
    'bagging_freq': 5,
    'verbose': -1
}

train_data = lgb.Dataset(X_train, label=y_train)
test_data = lgb.Dataset(X_test, label=y_test, reference=train_data)

model = lgb.train(
    params,
    train_data,
    num_boost_round=100,
    valid_sets=[test_data],
    valid_names=['test'],
    early_stopping_rounds=10
)

# Evaluate
from sklearn.metrics import accuracy_score, top_k_accuracy_score
preds = model.predict(X_test)
pred_classes = np.argmax(preds, axis=1)

accuracy = accuracy_score(y_test, pred_classes)
top3_accuracy = top_k_accuracy_score(y_test, preds, k=3)

print(f"Top-1 Accuracy: {accuracy:.4f}")
print(f"Top-3 Accuracy: {top3_accuracy:.4f}")
```

### 5. Export to ONNX Format

```python
import skl2onnx
from skl2onnx.common.data_types import FloatTensorType
from skl2onnx.common.data_types import Int64TensorType

# LightGBM doesn't have direct ONNX export; use onnxmltools
import onnxmltools

# Convert LightGBM to ONNX
initial_type = [('float_input', FloatTensorType([None, X.shape[1]]))]
onnx_model = onnxmltools.convert_lightgbm(model, initial_types=initial_type)

# Save ONNX model
onnxmltools.utils.save_model(onnx_model, 'inferred_region.onnx')
```

### 6. Generate Metadata JSON

Create the metadata file that maps class indices back to region IDs:

```python
import json

metadata = {
    'featureCount': X.shape[1],
    'featureNames': [
        'correlation_average_deviation',
        'correlation_pattern_similarity',
        'correlation_matching_asns',
        'correlation_has_history',
        'timezone_offset_hours',
        'is_vpn_or_proxy',
        'suspicion_score',
        'hour_of_day',
        'weekday',
        'locale_score'
    ],
    'classIndexToRegionId': {str(idx): region for idx, region in enumerate(le.classes_)},
    'regions': [
        {
            'regionId': region_id,
            'regionName': REGIONS_METADATA[region_id]['name'],
            'countryCode': REGIONS_METADATA[region_id]['country'],
            'latitude': REGIONS_METADATA[region_id]['lat'],
            'longitude': REGIONS_METADATA[region_id]['lon']
        }
        for region_id in le.classes_
    ]
}

with open('inferred_region-metadata.json', 'w') as f:
    json.dump(metadata, f, indent=2)
```

---

## Deployment

### 1. Copy Model Files

```bash
cp inferred_region.onnx /path/to/ask2ask.com/Models/
cp inferred_region-metadata.json /path/to/ask2ask.com/Models/
```

**Directory structure:**
```
ask2ask.com/
├── Models/
│   ├── inferred_region.onnx              # Binary ONNX model (~5-50MB)
│   ├── inferred_region-metadata.json    # Region class mapping
│   └── inferred_region-metadata.template.json  # Template reference
├── Services/
│   ├── OnnxInferredRegionEngine.cs
│   ├── InferredRegionEngine.cs
│   └── CompositeInferredRegionEngine.cs
└── ...
```

### 2. Verify Metadata

Ensure metadata JSON is valid:

```bash
jq . Models/inferred_region-metadata.json
```

Check:
- `featureCount` matches X.shape[1] from training
- `featureNames` order matches BuildFeatureVector() exactly
- `classIndexToRegionId` is complete (no gaps)
- All regions in classIndexToRegionId exist in `regions` array

### 3. Deploy Application

```bash
cd /path/to/ask2ask.com
dotnet restore
dotnet build
dotnet publish -c Release
```

The application will:
1. Detect ONNX model at startup
2. Load metadata and validate it
3. Use OnnxInferredRegionEngine for inference (if available)
4. Fall back to InferredRegionEngine (heuristic) if ONNX unavailable

### 4. Test Inference

Make a request to record a visit:

```bash
curl -X POST https://ask2ask.com/api/tracking \
  -H "Content-Type: application/json" \
  -d @request.json
```

Check logs for:
```
Loaded ONNX inferred region model with N regions
```

Query inferred region in visit response:
```bash
curl "https://ask2ask.com/api/visits?page=1" \
  -H "X-Api-Key: your-key" | jq '.data.Visits[0].InferredRegion'
```

---

## Model Updates & Retraining

**When to retrain:**
- Every 3-6 months (or quarterly) with accumulated new data
- After significant changes to VPN providers
- If accuracy drops below acceptable thresholds

**Workflow:**

1. **Export new data** (last 3 months)
2. **Merge with historical labeled data**
3. **Retrain model** (same pipeline as above)
4. **Evaluate on holdout test set** (ensure accuracy doesn't regress)
5. **Deploy new .onnx + metadata.json**
6. **Rolling restart** (no downtime if composite engine works)

---

## Troubleshooting

### Model not loading
```
ONNX engine not available, skipping model inference
```

**Check:**
- `Models/inferred_region.onnx` exists
- `Models/inferred_region-metadata.json` exists
- File permissions (readable by app process)
- Valid JSON in metadata file

### Feature count mismatch
```
Feature count mismatch: expected 10, got 8
```

**Fix:**
- Verify BuildFeatureVector() returns correct count
- Update metadata featureCount to match
- Retrain model with matching feature order

### Low inference accuracy
- Check training data quality (labels correct?)
- Verify feature engineering matches BuildFeatureVector() exactly
- Increase training set size (need ~500+ samples per region)
- Tune hyperparameters (num_leaves, learning_rate)

### VPN/Datacenter bias
- Separate training data by VPN vs non-VPN
- Train separate models if needed
- Use `flags.VpnExitMismatch` for post-hoc adjustments

---

## Performance Metrics to Track

```python
# Per-region metrics
for region in regions:
    indices = y_test == region
    if indices.sum() == 0:
        continue

    region_accuracy = accuracy_score(y_test[indices], pred_classes[indices])
    print(f"{region}: {region_accuracy:.4f} ({indices.sum()} samples)")

# Overall
print(f"Macro-average accuracy: {accuracy:.4f}")
print(f"Top-3 Accuracy: {top3_accuracy:.4f}")
print(f"Inference time per sample: {time_ms:.2f}ms")
```

---

## Example: End-to-End Training Script

See `scripts/train_inferred_region_model.py` for a complete example.

```bash
python scripts/train_inferred_region_model.py \
  --input ask2ask_visits.ndjson \
  --output Models/ \
  --test-size 0.2 \
  --num-rounds 100
```

---

## References

- [ONNX Runtime C# API](https://onnxruntime.ai/docs/api/csharp/)
- [LightGBM Python](https://lightgbm.readthedocs.io/)
- [onnxmltools](https://github.com/onnx/onnxmltools)
- [Ask2Ask Data Export API](../Pages/Api/Export.cshtml.cs)
