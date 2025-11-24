using System.ComponentModel.DataAnnotations;

namespace Ask2Ask.Data;

/// <summary>
/// Geographic region for inference
/// Represents a metro area or region used for geolocation inference
/// </summary>
public class Region
{
    [Key]
    public string RegionId { get; set; } = string.Empty; // "eu-ams", "us-nyc", etc.
    public string RegionName { get; set; } = string.Empty; // "Amsterdam Metro"
    public string CountryCode { get; set; } = string.Empty; // "NL", "US", etc.
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Optional metadata
    public string? Continent { get; set; }
    public int? Population { get; set; } // Metro population estimate
    public string? TimeZone { get; set; }
}

/// <summary>
/// Inferred geolocation result for a visit
/// Stores the output of the InferredRegionEngine
/// </summary>
public class InferredRegionResult
{
    public string RegionId { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Confidence { get; set; } // 0.0 to 1.0

    public List<RegionCandidate> Candidates { get; set; } = new();
    public InferredRegionFlags Flags { get; set; } = new();
}

public class RegionCandidate
{
    public string RegionId { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class InferredRegionFlags
{
    public bool VpnExitMismatch { get; set; }
    public bool GeoIpMismatch { get; set; }
    public bool TimezoneMismatch { get; set; }
}
