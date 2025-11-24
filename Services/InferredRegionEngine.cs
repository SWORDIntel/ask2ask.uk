using Ask2Ask.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ask2Ask.Services;

/// <summary>
/// Basic implementation of InferredRegionEngine
/// Uses heuristic-based inference from network timing, GeoIP, timezone, and ASN data
/// Can be extended with ML model (ONNX, ML.NET) for more sophisticated inference
/// </summary>
public class InferredRegionEngine : IInferredRegionEngine
{
    private readonly TrackingDbContext _context;
    private readonly ILogger<InferredRegionEngine> _logger;
    private readonly Dictionary<string, Region> _regionsCache;

    public InferredRegionEngine(TrackingDbContext context, ILogger<InferredRegionEngine> logger)
    {
        _context = context;
        _logger = logger;
        _regionsCache = new Dictionary<string, Region>();
    }

    public async Task<InferredRegionResult?> InferAsync(Visit visit, AsnPingCorrelation? correlation)
    {
        try
        {
            // Load regions if not cached
            if (_regionsCache.Count == 0)
            {
                await LoadRegionsAsync();
            }

            // If we have correlation data with inferred location, use it as primary signal
            if (correlation?.InferredCountry != null)
            {
                return BuildResultFromCorrelation(correlation);
            }

            // Otherwise, use heuristic inference from visit data
            return await InferFromVisitDataAsync(visit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during region inference");
            return null;
        }
    }

    private InferredRegionResult? BuildResultFromCorrelation(AsnPingCorrelation correlation)
    {
        // If correlation has inferred location, find matching region
        if (string.IsNullOrEmpty(correlation.InferredCountry))
            return null;

        // Find best matching region by country, then region, then distance
        var candidates = _regionsCache.Values
            .Where(r => r.CountryCode == correlation.InferredCountry)
            .ToList();

        if (!candidates.Any())
            return null;

        Region bestMatch;

        if (!string.IsNullOrEmpty(correlation.InferredCity) && candidates.Count > 1)
        {
            // Try to find by city name (fuzzy logic for robustness)
            var cityMatch = candidates.FirstOrDefault(r =>
                r.RegionName.Contains(correlation.InferredCity, StringComparison.OrdinalIgnoreCase));
            bestMatch = cityMatch ?? candidates[0];
        }
        else if (correlation.InferredLatitude.HasValue && correlation.InferredLongitude.HasValue)
        {
            // Find closest by distance
            bestMatch = candidates.OrderBy(r =>
                GeoDistance(r.Latitude, r.Longitude,
                           correlation.InferredLatitude.Value,
                           correlation.InferredLongitude.Value)
            ).First();
        }
        else
        {
            bestMatch = candidates[0];
        }

        var confidence = correlation.LocationConfidence ?? 0.5;

        return new InferredRegionResult
        {
            RegionId = bestMatch.RegionId,
            RegionName = bestMatch.RegionName,
            CountryCode = bestMatch.CountryCode,
            Latitude = bestMatch.Latitude,
            Longitude = bestMatch.Longitude,
            Confidence = confidence,
            Candidates = candidates
                .OrderByDescending(r => GetRegionScore(r, correlation))
                .Take(3)
                .Select(r => new RegionCandidate
                {
                    RegionId = r.RegionId,
                    Confidence = Math.Max(0.1, confidence * (r == bestMatch ? 1.0 : 0.5))
                })
                .ToList(),
            Flags = BuildFlags(bestMatch, correlation)
        };
    }

    private async Task<InferredRegionResult?> InferFromVisitDataAsync(Visit visit)
    {
        // Extract features from visit
        var timezone = visit.TimezoneOffset ?? 0;
        var countryHint = ExtractCountryHintFromLocale(visit.Locale);

        if (string.IsNullOrEmpty(countryHint))
            return null;

        // Find regions matching the country hint
        var candidates = _regionsCache.Values
            .Where(r => r.CountryCode == countryHint)
            .ToList();

        if (!candidates.Any())
            return null;

        // Score regions by timezone overlap
        var candidates_by_score = candidates
            .Select(r => new
            {
                Region = r,
                TimezoneScore = CalculateTimezoneScore(timezone, r.TimeZone),
                LocaleScore = ExtractLocaleScore(visit.Locale, r.RegionName)
            })
            .OrderByDescending(x => x.TimezoneScore + x.LocaleScore)
            .ToList();

        if (!candidates_by_score.Any())
            return null;

        var bestMatch = candidates_by_score[0].Region;
        var confidence = Math.Min(0.7, 0.3 + (candidates_by_score[0].TimezoneScore / 100.0));

        return new InferredRegionResult
        {
            RegionId = bestMatch.RegionId,
            RegionName = bestMatch.RegionName,
            CountryCode = bestMatch.CountryCode,
            Latitude = bestMatch.Latitude,
            Longitude = bestMatch.Longitude,
            Confidence = confidence,
            Candidates = candidates_by_score
                .Take(3)
                .Select(x => new RegionCandidate
                {
                    RegionId = x.Region.RegionId,
                    Confidence = Math.Max(0.1, confidence * (x.Region == bestMatch ? 1.0 : 0.5))
                })
                .ToList(),
            Flags = new InferredRegionFlags
            {
                VpnExitMismatch = false,
                GeoIpMismatch = false,
                TimezoneMismatch = false
            }
        };
    }

    private async Task LoadRegionsAsync()
    {
        var regions = await _context.Regions.ToListAsync();
        foreach (var region in regions)
        {
            _regionsCache[region.RegionId] = region;
        }

        if (regions.Count == 0)
        {
            _logger.LogWarning("No regions found in database. Region inference disabled.");
        }
    }

    private double GetRegionScore(Region region, AsnPingCorrelation correlation)
    {
        double score = 0;

        if (!string.IsNullOrEmpty(correlation.InferredCity) &&
            region.RegionName.Contains(correlation.InferredCity, StringComparison.OrdinalIgnoreCase))
        {
            score += 1.0;
        }

        if (correlation.InferredLatitude.HasValue && correlation.InferredLongitude.HasValue)
        {
            var distance = GeoDistance(
                region.Latitude, region.Longitude,
                correlation.InferredLatitude.Value,
                correlation.InferredLongitude.Value);
            score += Math.Max(0, 1.0 - (distance / 1000.0)); // Decay with distance in km
        }

        return score;
    }

    private InferredRegionFlags BuildFlags(Region region, AsnPingCorrelation correlation)
    {
        return new InferredRegionFlags
        {
            VpnExitMismatch = correlation.IsBehindVPN == true &&
                             correlation.InferredCountry != region.CountryCode,
            GeoIpMismatch = false, // Would need GeoIP data to set
            TimezoneMismatch = false // Would need timezone data to set
        };
    }

    private string? ExtractCountryHintFromLocale(string? locale)
    {
        if (string.IsNullOrEmpty(locale))
            return null;

        // Parse locale like "en-US", "fr-FR", "de-DE"
        var parts = locale.Split('-');
        if (parts.Length >= 2)
        {
            return parts[1].ToUpperInvariant();
        }

        return null;
    }

    private double CalculateTimezoneScore(int visitTimezoneOffset, string? regionTimeZone)
    {
        if (string.IsNullOrEmpty(regionTimeZone))
            return 50;

        // Try to parse region timezone (simplified UTC±HH:MM format)
        var expectedOffset = TimeZoneInfo.FindSystemTimeZoneById(regionTimeZone);
        var expectedOffsetMinutes = (int)expectedOffset.BaseUtcOffset.TotalMinutes;

        var delta = Math.Abs(visitTimezoneOffset - expectedOffsetMinutes);
        return Math.Max(0, 100 - delta); // Higher score for closer match
    }

    private double ExtractLocaleScore(string? locale, string regionName)
    {
        if (string.IsNullOrEmpty(locale))
            return 0;

        var parts = locale.Split('-');
        var language = parts[0].ToLowerInvariant();

        // Simple heuristic: boost score if region name is in common language for that locale
        // (This is a placeholder; a real system would have language-to-region mappings)
        return 10; // Neutral score
    }

    /// <summary>
    /// Calculate distance between two geographic points (Haversine formula)
    /// Returns distance in kilometers
    /// </summary>
    private double GeoDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusKm = 6371;

        var latDelta = DegreesToRadians(lat2 - lat1);
        var lonDelta = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(lonDelta / 2) * Math.Sin(lonDelta / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
