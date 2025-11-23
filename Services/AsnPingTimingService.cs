using Ask2Ask.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ask2Ask.Services;

/// <summary>
/// ASN Ping Timing Service
/// Processes ping timing measurements and correlates patterns across visits
/// </summary>
public class AsnPingTimingService
{
    private readonly TrackingDbContext _context;
    private readonly ILogger<AsnPingTimingService> _logger;
    private readonly AsnHelperService _asnHelper;

    public AsnPingTimingService(TrackingDbContext context, ILogger<AsnPingTimingService> logger, AsnHelperService? asnHelper = null)
        {
            _context = context;
            _logger = logger;
            _asnHelper = asnHelper ?? new AsnHelperService(logger);
        }

    /// <summary>
    /// Store ASN ping timing measurements for a visit
    /// </summary>
    public async Task StorePingTimingsAsync(int visitId, object pingTimingData)
    {
        try
        {
            var json = JsonSerializer.Serialize(pingTimingData);
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            if (!data.TryGetProperty("measurements", out var measurements))
            {
                _logger.LogWarning("No measurements found in ping timing data");
                return;
            }

            foreach (var measurement in measurements.EnumerateArray())
            {
                if (!measurement.TryGetProperty("success", out var success) || !success.GetBoolean())
                    continue;

                // Extract target IP for ASN enrichment.
                string? ip = null;
                if (measurement.TryGetProperty("target", out var targetProp))
                    ip = targetProp.GetString();

                uint? asnNumber = null;
                string? asnName = null;
                string? asnCountry = null;

                if (!string.IsNullOrWhiteSpace(ip))
                {
                    var asnInfo = await _asnHelper.QueryAsnInformationAsync(ip);
                    if (asnInfo != null)
                    {
                        asnNumber = asnInfo.Asn;
                        asnName = asnInfo.AsnName;
                        asnCountry = asnInfo.Country;
                    }
                }

                var pingTiming = new AsnPingTiming
                {
                    VisitId = visitId,
                    MeasuredAt = DateTime.UtcNow,
                    ASN = asnNumber?.ToString() ?? measurement.TryGetProperty("asn", out var asnProp) ? asnProp.GetString() ?? "" : "",
                    ASNName = asnName ?? measurement.TryGetProperty("asnName", out var asnNameProp) ? asnNameProp.GetString() ?? "" : "",
                    ASNCountry = asnCountry ?? measurement.TryGetProperty("country", out var countryProp) ? countryProp.GetString() : null,
                    ASNRegion = measurement.TryGetProperty("region", out var regionProp) ? regionProp.GetString() : null,
                    PingTarget = ip ?? "",
                    PingTargetType = measurement.TryGetProperty("targetType", out var targetTypeProp) ? targetTypeProp.GetString() ?? "" : "",
                    PingTime = measurement.TryGetProperty("average", out var avg) ? avg.GetDouble() : null,
                    MinPingTime = measurement.TryGetProperty("min", out var min) ? min.GetDouble() : null,
                    MaxPingTime = measurement.TryGetProperty("max", out var max) ? max.GetDouble() : null,
                    PingAttempts = measurement.TryGetProperty("attempts", out var attempts) ? attempts.GetInt32() : null,
                    SuccessfulPings = measurement.TryGetProperty("successful", out var successful) ? successful.GetInt32() : null,
                    Jitter = measurement.TryGetProperty("jitter", out var jitter) ? jitter.GetDouble() : null,
                    RawData = JsonSerializer.Serialize(measurement)
                };

                _context.AsnPingTimings.Add(pingTiming);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Stored {measurements.GetArrayLength()} ASN ping timing measurements for visit {visitId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store ASN ping timing data");
        }
    }

    /// <summary>
    /// Create ping pattern signature from measurements
    /// </summary>
    public string CreatePatternHash(object pingPatternData)
    {
        try
        {
            var json = JsonSerializer.Serialize(pingPatternData);
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            if (!data.TryGetProperty("pattern", out var pattern))
                return string.Empty;

            // Create normalized pattern string for hashing
            var patternString = new StringBuilder();
            foreach (var item in pattern.EnumerateArray())
            {
                var asn = item.TryGetProperty("asn", out var asnProp) ? asnProp.GetString() ?? "" : "";
                var normalizedTime = item.TryGetProperty("normalizedTime", out var normTime) ? normTime.GetDouble() : 0;
                patternString.Append($"{asn}:{normalizedTime:F4};");
            }

            // Compute SHA-384 hash (CNSA 2.0 compliant)
            using var sha384 = SHA384.Create();
            var hashBytes = sha384.ComputeHash(Encoding.UTF8.GetBytes(patternString.ToString()));
            return Convert.ToBase64String(hashBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ping pattern hash");
            return string.Empty;
        }
    }

    /// <summary>
    /// Correlate ping patterns across visits for a visitor
    /// </summary>
    public async Task<AsnPingCorrelation?> CorrelatePingPatternsAsync(int visitorId, string patternHash, object pingPatternData)
    {
        try
        {
            // Check if pattern already exists for this visitor
            var existingCorrelation = await _context.AsnPingCorrelations
                .FirstOrDefaultAsync(c => c.VisitorId == visitorId && c.PatternHash == patternHash);

            if (existingCorrelation != null)
            {
                // Update existing correlation
                existingCorrelation.LastSeen = DateTime.UtcNow;
                existingCorrelation.VisitCount++;
                existingCorrelation.PatternData = JsonSerializer.Serialize(pingPatternData);
                
                // Recalculate location inference
                await InferLocationAsync(existingCorrelation);
                
                await _context.SaveChangesAsync();
                return existingCorrelation;
            }

            // Check for similar patterns (within threshold)
            var similarPattern = await FindSimilarPatternAsync(visitorId, patternHash, pingPatternData);
            
            if (similarPattern != null)
            {
                // Update similar pattern
                similarPattern.LastSeen = DateTime.UtcNow;
                similarPattern.VisitCount++;
                await InferLocationAsync(similarPattern);
                await _context.SaveChangesAsync();
                return similarPattern;
            }

            // Create new correlation
            var newCorrelation = new AsnPingCorrelation
            {
                VisitorId = visitorId,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                VisitCount = 1,
                PatternHash = patternHash,
                PatternData = JsonSerializer.Serialize(pingPatternData)
            };

            await InferLocationAsync(newCorrelation);
            
            _context.AsnPingCorrelations.Add(newCorrelation);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created new ping pattern correlation for visitor {visitorId}");
            return newCorrelation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to correlate ping patterns");
            return null;
        }
    }

    /// <summary>
    /// Find similar ping patterns (for correlation across VPN usage)
    /// </summary>
    private async Task<AsnPingCorrelation?> FindSimilarPatternAsync(int visitorId, string patternHash, object pingPatternData)
    {
        // Get all patterns for this visitor
        var existingPatterns = await _context.AsnPingCorrelations
            .Where(c => c.VisitorId == visitorId)
            .ToListAsync();

        if (existingPatterns.Count == 0)
            return null;

        var currentPattern = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(pingPatternData));
        if (!currentPattern.TryGetProperty("pattern", out var currentPatternArray))
            return null;

        // Compare with existing patterns
        foreach (var existing in existingPatterns)
        {
            try
            {
                var existingPatternJson = JsonSerializer.Deserialize<JsonElement>(existing.PatternData);
                if (!existingPatternJson.TryGetProperty("pattern", out var existingPatternArray))
                    continue;

                var similarity = CalculatePatternSimilarity(currentPatternArray, existingPatternArray);
                
                // If similarity > 0.7, consider it the same pattern (even if behind VPN)
                if (similarity > 0.7)
                {
                    existing.PatternSimilarity = similarity;
                    return existing;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to compare pattern with existing correlation {existing.Id}");
            }
        }

        return null;
    }

    /// <summary>
    /// Calculate similarity between two ping patterns
    /// </summary>
    private double CalculatePatternSimilarity(JsonElement pattern1, JsonElement pattern2)
    {
        try
        {
            var p1 = pattern1.EnumerateArray().ToList();
            var p2 = pattern2.EnumerateArray().ToList();

            if (p1.Count == 0 || p2.Count == 0)
                return 0.0;

            // Find common ASNs
            var commonASNs = new HashSet<string>();
            foreach (var item in p1)
            {
                if (item.TryGetProperty("asn", out var asn))
                    commonASNs.Add(asn.GetString() ?? "");
            }

            var matchingASNs = 0;
            var totalDeviation = 0.0;
            var comparisons = 0;

            foreach (var item1 in p1)
            {
                if (!item1.TryGetProperty("asn", out var asn1))
                    continue;

                var asn1Str = asn1.GetString() ?? "";
                var normTime1 = item1.TryGetProperty("normalizedTime", out var nt1) ? nt1.GetDouble() : 0;

                foreach (var item2 in p2)
                {
                    if (!item2.TryGetProperty("asn", out var asn2))
                        continue;

                    var asn2Str = asn2.GetString() ?? "";
                    if (asn1Str == asn2Str)
                    {
                        matchingASNs++;
                        var normTime2 = item2.TryGetProperty("normalizedTime", out var nt2) ? nt2.GetDouble() : 0;
                        var deviation = Math.Abs(normTime1 - normTime2);
                        totalDeviation += deviation;
                        comparisons++;
                        break;
                    }
                }
            }

            if (comparisons == 0)
                return 0.0;

            // Similarity based on matching ASNs and low deviation
            var asnMatchRatio = (double)matchingASNs / Math.Max(p1.Count, p2.Count);
            var avgDeviation = totalDeviation / comparisons;
            var deviationScore = Math.Max(0, 1.0 - (avgDeviation * 2)); // Lower deviation = higher score

            var similarity = (asnMatchRatio * 0.6) + (deviationScore * 0.4);
            return Math.Min(1.0, similarity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate pattern similarity");
            return 0.0;
        }
    }

    /// <summary>
    /// Infer location from ping timing pattern
    /// </summary>
    private async Task InferLocationAsync(AsnPingCorrelation correlation)
    {
        try
        {
            var patternData = JsonSerializer.Deserialize<JsonElement>(correlation.PatternData);
            if (!patternData.TryGetProperty("pattern", out var pattern))
                return;

            // Find ASNs with fastest ping times (likely closest to user)
            var fastestASNs = pattern.EnumerateArray()
                .OrderBy(p => p.TryGetProperty("normalizedTime", out var nt) ? nt.GetDouble() : double.MaxValue)
                .Take(5)
                .ToList();

            if (fastestASNs.Count == 0)
                return;

            // Get most common country/region from fastest ASNs
            var countries = fastestASNs
                .Where(p => p.TryGetProperty("country", out var c) && c.GetString() != "Global")
                .Select(p => p.TryGetProperty("country", out var c) ? c.GetString() : null)
                .Where(c => !string.IsNullOrEmpty(c))
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .ToList();

            if (countries.Count > 0)
            {
                correlation.InferredCountry = countries[0].Key;
                
                var regions = fastestASNs
                    .Where(p => p.TryGetProperty("region", out var r) && r.GetString() != null)
                    .Select(p => p.TryGetProperty("region", out var r) ? r.GetString() : null)
                    .Where(r => !string.IsNullOrEmpty(r))
                    .GroupBy(r => r)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                if (regions.Count > 0)
                {
                    correlation.InferredRegion = regions[0].Key;
                }

                // Calculate confidence based on consistency
                var consistentASNs = countries[0].Count();
                correlation.LocationConfidence = Math.Min(1.0, consistentASNs / 5.0);
                correlation.MatchingASNs = consistentASNs;
            }

            // Check if visitor is behind VPN (ping times inconsistent with IP geolocation)
            var visit = await _context.Visits
                .Include(v => v.VPNProxyDetection)
                .Where(v => v.VisitorId == correlation.VisitorId)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefaultAsync();

            if (visit?.VPNProxyDetection != null && visit.VPNProxyDetection.IsLikelyVPNOrProxy)
            {
                correlation.IsBehindVPN = true;
                correlation.OriginalLocation = correlation.InferredCountry; // Inferred location despite VPN
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to infer location from ping pattern");
        }
    }

    /// <summary>
    /// Get ping pattern correlations for a visitor
    /// </summary>
    public async Task<List<AsnPingCorrelation>> GetVisitorCorrelationsAsync(int visitorId)
    {
        return await _context.AsnPingCorrelations
            .Where(c => c.VisitorId == visitorId)
            .OrderByDescending(c => c.LastSeen)
            .ToListAsync();
    }
}

