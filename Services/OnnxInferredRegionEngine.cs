using Ask2Ask.Data;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.Json;

namespace Ask2Ask.Services;

/// <summary>
/// ONNX Runtime-based InferredRegionEngine
/// Uses a trained ML model (LightGBM/XGBoost exported to ONNX) for region classification.
/// Requires: Models/inferred_region.onnx and Models/inferred_region-metadata.json
/// </summary>
public class OnnxInferredRegionEngine : IInferredRegionEngine, IDisposable
{
    private readonly ILogger<OnnxInferredRegionEngine> _logger;
    private readonly InferenceSession? _session;
    private readonly ModelMetadata? _metadata;
    private readonly Dictionary<string, Region> _regionsCache;

    public OnnxInferredRegionEngine(ILogger<OnnxInferredRegionEngine> logger, TrackingDbContext context)
    {
        _logger = logger;
        _regionsCache = new Dictionary<string, Region>();

        try
        {
            var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "inferred_region.onnx");
            var metaPath = Path.Combine(AppContext.BaseDirectory, "Models", "inferred_region-metadata.json");

            if (!File.Exists(modelPath))
            {
                _logger.LogWarning("ONNX model not found at {Path}. Running in fallback mode.", modelPath);
                return;
            }

            if (!File.Exists(metaPath))
            {
                _logger.LogWarning("Model metadata not found at {Path}. Running in fallback mode.", metaPath);
                return;
            }

            _session = new InferenceSession(modelPath);

            var metaJson = File.ReadAllText(metaPath);
            _metadata = JsonSerializer.Deserialize<ModelMetadata>(metaJson);

            if (_metadata == null || _metadata.Regions.Count == 0)
            {
                _logger.LogWarning("Invalid or empty model metadata. Running in fallback mode.");
                _session.Dispose();
                _session = null;
                return;
            }

            // Cache regions
            foreach (var region in _metadata.Regions)
            {
                _regionsCache[region.RegionId] = region;
            }

            _logger.LogInformation("Loaded ONNX inferred region model with {RegionCount} regions", _metadata.Regions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load ONNX model. Running in fallback mode.");
            _session?.Dispose();
            _session = null;
            _metadata = null;
        }
    }

    public async Task<InferredRegionResult?> InferAsync(Visit visit, AsnPingCorrelation? correlation)
    {
        if (_session == null || _metadata == null)
        {
            _logger.LogDebug("ONNX engine not available, skipping model inference");
            return null;
        }

        try
        {
            var features = BuildFeatureVector(visit, correlation);
            if (features == null || features.Length == 0)
            {
                return null;
            }

            var inputName = _session.InputMetadata.Keys.First();
            var outputName = _session.OutputMetadata.Keys.First();

            // Create input tensor (batch size 1)
            var tensor = new DenseTensor<float>(new[] { 1, features.Length });
            for (int i = 0; i < features.Length; i++)
            {
                tensor[0, i] = features[i];
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First(v => v.Name == outputName).AsEnumerable<float>().ToArray();

            if (output.Length == 0)
                return null;

            // Apply softmax to get probabilities
            var probs = Softmax(output);
            var indexed = probs
                .Select((p, idx) => new { Index = idx, Prob = p })
                .OrderByDescending(x => x.Prob)
                .ToArray();

            var topIndex = indexed[0].Index;
            var topConfidence = indexed[0].Prob;

            if (topIndex < 0 || topIndex >= _metadata.ClassIndexToRegionId.Count)
            {
                _logger.LogWarning("Invalid class index {Index} from model output", topIndex);
                return null;
            }

            var regionId = _metadata.ClassIndexToRegionId[topIndex];
            if (!_regionsCache.TryGetValue(regionId, out var region))
            {
                _logger.LogWarning("Region {RegionId} not found in metadata", regionId);
                return null;
            }

            // Build top candidates
            var candidates = indexed
                .Take(Math.Min(5, indexed.Length))
                .Where(x => x.Index >= 0 && x.Index < _metadata.ClassIndexToRegionId.Count)
                .Select(x =>
                {
                    var cRegionId = _metadata.ClassIndexToRegionId[x.Index];
                    return new RegionCandidate
                    {
                        RegionId = cRegionId,
                        Confidence = x.Prob
                    };
                })
                .ToList();

            var flags = ComputeFlags(visit, region);

            var result = new InferredRegionResult
            {
                RegionId = region.RegionId,
                RegionName = region.RegionName,
                CountryCode = region.CountryCode,
                Latitude = region.Latitude,
                Longitude = region.Longitude,
                Confidence = topConfidence,
                Candidates = candidates,
                Flags = flags
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ONNX region inference for visit {VisitId}", visit.Id);
            return null;
        }
    }

    /// <summary>
    /// Build feature vector for ONNX model input.
    /// Must match the exact feature order used during model training.
    /// </summary>
    private float[]? BuildFeatureVector(Visit visit, AsnPingCorrelation? correlation)
    {
        if (_metadata == null)
            return null;

        var features = new List<float>();

        // Network features from ASN ping correlation
        if (correlation != null)
        {
            // Normalized RTT values (median, min, max, jitter, success rate)
            // These should match the exact probe order from training
            features.Add(NormalizeRtt(correlation.AverageDeviation ?? 0));
            features.Add(NormalizeRtt(correlation.PatternSimilarity ?? 0));
            features.Add((float)(correlation.MatchingASNs ?? 0) / 50f); // Normalize by typical max
            features.Add((float)(correlation.VisitCount > 0 ? 1.0 : 0.0)); // Has history
        }
        else
        {
            // Missing correlation: zero padding
            features.AddRange(new[] { 0f, 0f, 0f, 0f });
        }

        // Timezone offset (normalize to hours)
        var tzHours = visit.TimezoneOffset.HasValue ? visit.TimezoneOffset.Value / 60f : 0f;
        features.Add(Math.Max(-12f, Math.Min(12f, tzHours)) / 12f); // Clamp to [-1, 1]

        // VPN detection flag
        var vpnProxyDetection = visit.VPNProxyDetection;
        features.Add(vpnProxyDetection?.IsLikelyVPNOrProxy == true ? 1f : 0f);

        // Suspicion level encoding
        var suspicionScore = vpnProxyDetection?.SuspicionLevel switch
        {
            "high" => 1.0f,
            "medium" => 0.5f,
            "low" => 0.25f,
            _ => 0.0f
        };
        features.Add(suspicionScore);

        // Hour of day (if available from request time)
        var hour = visit.Timestamp.Hour;
        features.Add(hour / 24f); // Normalize to [0, 1]

        // Weekday (0=Sunday, 6=Saturday)
        var weekday = (int)visit.Timestamp.DayOfWeek;
        features.Add(weekday / 7f);

        // Browser language/locale features
        var localeScore = ExtractLocaleScore(visit.Locale);
        features.Add(localeScore);

        if (features.Count != _metadata.FeatureCount)
        {
            _logger.LogWarning(
                "Feature count mismatch: expected {Expected}, got {Actual}",
                _metadata.FeatureCount,
                features.Count);
            return null;
        }

        return features.ToArray();
    }

    private float NormalizeRtt(double rttMs)
    {
        // Normalize RTT: assume typical range 10-500ms
        return (float)Math.Max(0, Math.Min(1, (rttMs - 10) / 490));
    }

    private float ExtractLocaleScore(string? locale)
    {
        if (string.IsNullOrEmpty(locale))
            return 0f;

        // Simple heuristic: some locales are more common in certain regions
        // In a real system, this would be learned from data
        return locale.Length > 2 ? 0.5f : 0f;
    }

    private InferredRegionFlags ComputeFlags(Visit visit, Region region)
    {
        var flags = new InferredRegionFlags();

        var vpnDetection = visit.VPNProxyDetection;
        if (vpnDetection != null && !string.IsNullOrEmpty(vpnDetection.Country))
        {
            flags.VpnExitMismatch = !string.Equals(vpnDetection.Country, region.CountryCode, StringComparison.OrdinalIgnoreCase);
        }

        // GeoIP check would require additional data
        flags.GeoIpMismatch = false; // Placeholder

        // Timezone mismatch check
        if (visit.TimezoneOffset.HasValue)
        {
            var tzHours = visit.TimezoneOffset.Value / 60.0;
            var expectedTzHours = Math.Round(region.Longitude / 15.0); // 15° per hour
            flags.TimezoneMismatch = Math.Abs(tzHours - expectedTzHours) > 2.5; // > 2.5 hour difference
        }

        return flags;
    }

    private static float[] Softmax(float[] logits)
    {
        var max = logits.Max();
        var exps = logits.Select(l => MathF.Exp(l - max)).ToArray();
        var sum = exps.Sum();
        if (sum <= 0) sum = 1f;

        for (int i = 0; i < exps.Length; i++)
        {
            exps[i] /= sum;
        }

        return exps;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }

    /// <summary>
    /// Model metadata: feature names, class-to-region mapping, regions list
    /// </summary>
    private sealed class ModelMetadata
    {
        public int FeatureCount { get; set; }
        public List<string> FeatureNames { get; set; } = new();
        public Dictionary<int, string> ClassIndexToRegionId { get; set; } = new();
        public List<Region> Regions { get; set; } = new();
    }
}
