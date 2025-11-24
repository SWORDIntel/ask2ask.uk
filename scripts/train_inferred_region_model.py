#!/usr/bin/env python3
"""
Train ONNX model for InferredRegionEngine

Usage:
    python train_inferred_region_model.py \
      --input ask2ask_visits.ndjson \
      --output Models/ \
      --test-size 0.2 \
      --num-rounds 100
"""

import argparse
import json
import logging
import sys
from pathlib import Path

import numpy as np
import pandas as pd

try:
    import lightgbm as lgb
    import onnxmltools
    from sklearn.model_selection import train_test_split
    from sklearn.preprocessing import LabelEncoder
    from sklearn.metrics import accuracy_score, top_k_accuracy_score
except ImportError as e:
    print(f"Missing required package: {e}")
    print("Install with: pip install lightgbm onnxmltools scikit-learn pandas numpy")
    sys.exit(1)

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Global region definitions (must match Models/Regions.json)
REGION_DEFINITIONS = {
    'eu-ams': {'name': 'Amsterdam Metro', 'country': 'NL', 'lat': 52.3676, 'lon': 4.9041},
    'eu-bru': {'name': 'Brussels Metro', 'country': 'BE', 'lat': 50.8503, 'lon': 4.3517},
    'eu-fra': {'name': 'Frankfurt Metro', 'country': 'DE', 'lat': 50.1109, 'lon': 8.6821},
    'eu-ber': {'name': 'Berlin Metro', 'country': 'DE', 'lat': 52.5200, 'lon': 13.4050},
    'eu-par': {'name': 'Paris Metro', 'country': 'FR', 'lat': 48.8566, 'lon': 2.3522},
    'eu-lon': {'name': 'London Metro', 'country': 'GB', 'lat': 51.5074, 'lon': -0.1278},
    'us-nyc': {'name': 'New York Metro', 'country': 'US', 'lat': 40.7128, 'lon': -74.0060},
    'us-lax': {'name': 'Los Angeles Metro', 'country': 'US', 'lat': 34.0522, 'lon': -118.2437},
    'us-chi': {'name': 'Chicago Metro', 'country': 'US', 'lat': 41.8781, 'lon': -87.6298},
    'ap-tok': {'name': 'Tokyo Metro', 'country': 'JP', 'lat': 35.6762, 'lon': 139.6503},
}

class ModelTrainer:
    """Train and export ONNX region classification model"""

    def __init__(self, output_dir: str):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        self.label_encoder = None

    def load_data(self, input_file: str) -> pd.DataFrame:
        """Load NDJSON export from Ask2Ask API"""
        logger.info(f"Loading data from {input_file}")
        records = []

        with open(input_file, 'r') as f:
            for line in f:
                if not line.strip():
                    continue
                try:
                    record = json.loads(line)
                    records.append(record)
                except json.JSONDecodeError as e:
                    logger.warning(f"Skipping invalid JSON: {e}")
                    continue

        df = pd.DataFrame(records)
        logger.info(f"Loaded {len(df)} records")
        return df

    def extract_label(self, row: dict) -> str:
        """Extract ground truth region from visit record"""
        # Priority: GPS > GeoIP city > ASN inferred > None
        if row.get('latitude') and row.get('longitude'):
            # Find nearest region (simplified)
            return self.nearest_region_by_coords(
                row['latitude'], row['longitude']
            )

        if row.get('geoip_city'):
            region = self.find_region_by_city(row['geoip_city'])
            if region:
                return region

        if row.get('asn_inferred_city'):
            region = self.find_region_by_city(row['asn_inferred_city'])
            if region:
                return region

        return None

    def nearest_region_by_coords(self, lat: float, lon: float) -> str:
        """Find nearest region by coordinates"""
        best_region = None
        best_distance = float('inf')

        for region_id, meta in REGION_DEFINITIONS.items():
            distance = (
                (lat - meta['lat']) ** 2 +
                (lon - meta['lon']) ** 2
            ) ** 0.5
            if distance < best_distance:
                best_distance = distance
                best_region = region_id

        return best_region if best_distance < 100 else None  # Max ~10° (~1000km)

    def find_region_by_city(self, city: str) -> str:
        """Fuzzy match city to region (simplified)"""
        city_lower = city.lower()
        for region_id, meta in REGION_DEFINITIONS.items():
            if city_lower in meta['name'].lower():
                return region_id
        return None

    def build_features(self, row: dict) -> np.ndarray:
        """
        Build feature vector from visit record.
        MUST MATCH BuildFeatureVector() in OnnxInferredRegionEngine.cs
        """
        features = []

        # ASN correlation features
        correlation = row.get('asn_correlation', {})
        if correlation:
            features.append(self.normalize_rtt(
                correlation.get('average_deviation', 0)
            ))
            features.append(correlation.get('pattern_similarity', 0))
            features.append(correlation.get('matching_asns', 0) / 50.0)
            features.append(1.0 if correlation.get('visit_count', 0) > 0 else 0.0)
        else:
            features.extend([0.0, 0.0, 0.0, 0.0])

        # Timezone (normalized hours)
        tz_minutes = row.get('timezone_offset_minutes', 0)
        tz_hours = tz_minutes / 60.0 if tz_minutes else 0.0
        tz_normalized = max(-1.0, min(1.0, tz_hours / 12.0))
        features.append(tz_normalized)

        # VPN detection
        vpn = row.get('vpn_detection', {})
        features.append(1.0 if vpn.get('is_likely_vpn', False) else 0.0)

        # Suspicion level
        suspicion_level = vpn.get('suspicion_level', 'low')
        suspicion_score = {
            'high': 1.0,
            'medium': 0.5,
            'low': 0.25
        }.get(suspicion_level, 0.0)
        features.append(suspicion_score)

        # Time features
        timestamp = pd.to_datetime(row.get('timestamp', pd.Timestamp.now()))
        features.append(timestamp.hour / 24.0)
        features.append(timestamp.weekday() / 7.0)

        # Locale feature
        locale = row.get('locale', '')
        locale_score = 0.5 if locale and len(locale) > 2 else 0.0
        features.append(locale_score)

        return np.array(features)

    @staticmethod
    def normalize_rtt(rtt_ms: float) -> float:
        """Normalize RTT to [0, 1]"""
        return max(0.0, min(1.0, (rtt_ms - 10) / 490))

    def prepare_training_data(
        self, df: pd.DataFrame
    ) -> tuple[np.ndarray, np.ndarray, LabelEncoder]:
        """Prepare features and labels for training"""
        logger.info("Preparing training data...")

        features = []
        labels = []
        skipped = 0

        for _, row in df.iterrows():
            label = self.extract_label(row)
            if not label:
                skipped += 1
                continue

            feat = self.build_features(row)
            features.append(feat)
            labels.append(label)

        X = np.array(features)
        le = LabelEncoder()
        y = le.fit_transform(labels)

        logger.info(f"Prepared {len(X)} samples (skipped {skipped})")
        logger.info(f"Regions: {list(le.classes_)}")
        logger.info(f"Sample distribution:\n{pd.Series(labels).value_counts()}")

        return X, y, le

    def train(
        self,
        X: np.ndarray,
        y: np.ndarray,
        test_size: float = 0.2,
        num_rounds: int = 100
    ) -> lgb.Booster:
        """Train LightGBM classifier"""
        logger.info("Splitting data...")
        X_train, X_test, y_train, y_test = train_test_split(
            X, y, test_size=test_size, random_state=42, stratify=y
        )

        num_classes = len(np.unique(y))
        logger.info(f"Training LightGBM with {num_classes} classes...")

        params = {
            'objective': 'multiclass',
            'num_class': num_classes,
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
            num_boost_round=num_rounds,
            valid_sets=[test_data],
            valid_names=['test'],
            early_stopping_rounds=10
        )

        logger.info("Training complete")

        # Evaluate
        preds = model.predict(X_test)
        pred_classes = np.argmax(preds, axis=1)

        accuracy = accuracy_score(y_test, pred_classes)
        top3_accuracy = top_k_accuracy_score(y_test, preds, k=min(3, num_classes))

        logger.info(f"Top-1 Accuracy: {accuracy:.4f}")
        logger.info(f"Top-3 Accuracy: {top3_accuracy:.4f}")

        return model, X_test, y_test

    def export_onnx(self, model: lgb.Booster, X: np.ndarray, y: np.ndarray, le: LabelEncoder):
        """Export LightGBM model to ONNX format"""
        logger.info("Exporting to ONNX...")

        try:
            from skl2onnx.common.data_types import FloatTensorType

            initial_type = [('float_input', FloatTensorType([None, X.shape[1]]))]
            onnx_model = onnxmltools.convert_lightgbm(
                model, initial_types=initial_type
            )

            model_path = self.output_dir / "inferred_region.onnx"
            onnxmltools.utils.save_model(onnx_model, str(model_path))
            logger.info(f"Saved ONNX model to {model_path}")

            return model_path
        except Exception as e:
            logger.error(f"Failed to export ONNX: {e}")
            raise

    def save_metadata(self, le: LabelEncoder, feature_count: int):
        """Generate metadata JSON for ONNX model"""
        logger.info("Generating metadata...")

        feature_names = [
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
        ]

        assert len(feature_names) == feature_count, \
            f"Feature count mismatch: {len(feature_names)} != {feature_count}"

        metadata = {
            'featureCount': feature_count,
            'featureNames': feature_names,
            'classIndexToRegionId': {
                str(idx): region for idx, region in enumerate(le.classes_)
            },
            'regions': [
                {
                    'regionId': region_id,
                    'regionName': REGION_DEFINITIONS[region_id]['name'],
                    'countryCode': REGION_DEFINITIONS[region_id]['country'],
                    'latitude': REGION_DEFINITIONS[region_id]['lat'],
                    'longitude': REGION_DEFINITIONS[region_id]['lon']
                }
                for region_id in le.classes_
                if region_id in REGION_DEFINITIONS
            ]
        }

        metadata_path = self.output_dir / "inferred_region-metadata.json"
        with open(metadata_path, 'w') as f:
            json.dump(metadata, f, indent=2)

        logger.info(f"Saved metadata to {metadata_path}")

    def run(self, input_file: str, test_size: float = 0.2, num_rounds: int = 100):
        """End-to-end training pipeline"""
        df = self.load_data(input_file)
        X, y, le = self.prepare_training_data(df)

        if len(X) < 50:
            logger.error("Not enough samples for training")
            return False

        model, X_test, y_test = self.train(X, y, test_size=test_size, num_rounds=num_rounds)
        self.export_onnx(model, X, y, le)
        self.save_metadata(le, X.shape[1])

        logger.info("Training complete!")
        return True


def main():
    parser = argparse.ArgumentParser(
        description='Train ONNX model for Ask2Ask InferredRegionEngine'
    )
    parser.add_argument('--input', required=True, help='Input NDJSON file from /api/export')
    parser.add_argument('--output', required=True, help='Output directory for ONNX model')
    parser.add_argument('--test-size', type=float, default=0.2, help='Test set fraction')
    parser.add_argument('--num-rounds', type=int, default=100, help='LightGBM boosting rounds')

    args = parser.parse_args()

    trainer = ModelTrainer(args.output)
    success = trainer.run(
        input_file=args.input,
        test_size=args.test_size,
        num_rounds=args.num_rounds
    )

    return 0 if success else 1


if __name__ == '__main__':
    sys.exit(main())
