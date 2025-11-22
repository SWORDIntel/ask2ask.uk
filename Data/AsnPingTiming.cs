using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ask2Ask.Data;

/// <summary>
/// ASN Ping Timing Data
/// Stores ping timing measurements to various ASNs for location inference
/// </summary>
public class AsnPingTiming
{
    [Key]
    public int Id { get; set; }
    
    public int VisitId { get; set; }
    [ForeignKey("VisitId")]
    public Visit Visit { get; set; } = null!;
    
    public DateTime MeasuredAt { get; set; }
    
    // ASN Information
    public string ASN { get; set; } = string.Empty; // e.g., "AS15169" (Google)
    public string ASNName { get; set; } = string.Empty; // e.g., "Google LLC"
    public string? ASNCountry { get; set; } // ISO country code
    public string? ASNRegion { get; set; } // Region/state
    public string? ASNCity { get; set; } // City
    
    // Ping Target
    public string PingTarget { get; set; } = string.Empty; // IP or hostname
    public string PingTargetType { get; set; } = string.Empty; // "IP", "Hostname", "CDN"
    
    // Timing Measurements (in milliseconds)
    public double? PingTime { get; set; } // Average ping time
    public double? MinPingTime { get; set; } // Minimum ping time
    public double? MaxPingTime { get; set; } // Maximum ping time
    public int? PingAttempts { get; set; } // Number of ping attempts
    public int? SuccessfulPings { get; set; } // Number of successful pings
    public double? Jitter { get; set; } // Jitter (variance in ping times)
    
    // Network Path Information
    public string? TracerouteHops { get; set; } // JSON array of hop IPs
    public int? HopCount { get; set; } // Number of hops to target
    
    // Metadata
    public string? MeasurementMethod { get; set; } // "WebSocket", "Image", "Fetch", "Beacon"
    public string? RawData { get; set; } // JSON with full measurement data
}

/// <summary>
/// ASN Ping Pattern Correlation
/// Correlates ping patterns across multiple visits to identify same location
/// </summary>
public class AsnPingCorrelation
{
    [Key]
    public int Id { get; set; }
    
    public int VisitorId { get; set; }
    [ForeignKey("VisitorId")]
    public Visitor Visitor { get; set; } = null!;
    
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int VisitCount { get; set; } // Number of visits with this pattern
    
    // Pattern Signature
    public string PatternHash { get; set; } = string.Empty; // SHA-384 hash of ping pattern
    public string PatternData { get; set; } = string.Empty; // JSON with normalized ping times
    
    // Inferred Location
    public string? InferredCountry { get; set; }
    public string? InferredRegion { get; set; }
    public string? InferredCity { get; set; }
    public double? InferredLatitude { get; set; }
    public double? InferredLongitude { get; set; }
    public double? LocationConfidence { get; set; } // 0.0 to 1.0
    
    // Correlation Metrics
    public double? PatternSimilarity { get; set; } // Similarity score across visits
    public int? MatchingASNs { get; set; } // Number of ASNs with consistent timing
    public double? AverageDeviation { get; set; } // Average deviation from pattern
    
    // VPN Detection
    public bool? IsBehindVPN { get; set; } // Detected VPN usage
    public string? VPNProvider { get; set; } // Detected VPN provider
    public string? OriginalLocation { get; set; } // Inferred original location despite VPN
}

