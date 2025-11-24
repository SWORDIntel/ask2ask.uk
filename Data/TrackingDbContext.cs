using Microsoft.EntityFrameworkCore;

namespace Ask2Ask.Data;

public class TrackingDbContext : DbContext
{
    public TrackingDbContext(DbContextOptions<TrackingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Visitor> Visitors { get; set; }
    public DbSet<Visit> Visits { get; set; }
    public DbSet<VPNProxyDetection> VPNProxyDetections { get; set; }
    public DbSet<AsnPingTiming> AsnPingTimings { get; set; }
    public DbSet<AsnPingCorrelation> AsnPingCorrelations { get; set; }
    public DbSet<Region> Regions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure indexes for better query performance
        modelBuilder.Entity<Visitor>()
            .HasIndex(v => v.FingerprintHash)
            .IsUnique();

        modelBuilder.Entity<Visit>()
            .HasIndex(v => v.VisitorId);

        modelBuilder.Entity<Visit>()
            .HasIndex(v => v.Timestamp);

        modelBuilder.Entity<VPNProxyDetection>()
            .HasIndex(v => v.VisitId);

        modelBuilder.Entity<AsnPingTiming>()
            .HasIndex(a => a.VisitId);

        modelBuilder.Entity<AsnPingTiming>()
            .HasIndex(a => a.ASN);

        modelBuilder.Entity<AsnPingCorrelation>()
            .HasIndex(a => a.VisitorId);

        modelBuilder.Entity<AsnPingCorrelation>()
            .HasIndex(a => a.PatternHash);
    }
}

public class Visitor
{
    public int Id { get; set; }
    public string FingerprintHash { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int VisitCount { get; set; }
    public string? UserAgent { get; set; }
    public string? Platform { get; set; }
    public string? Language { get; set; }
    
    // Navigation property
    public ICollection<Visit> Visits { get; set; } = new List<Visit>();
}

public class Visit
{
    public int Id { get; set; }
    public int VisitorId { get; set; }
    public DateTime Timestamp { get; set; }
    public string SessionId { get; set; } = string.Empty;
    
    // IP Information
    public string? RemoteIP { get; set; }
    public string? ForwardedFor { get; set; }
    public string? RealIP { get; set; }
    
    // Request Information
    public string? UserAgent { get; set; }
    public string? Referer { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    
    // Browser Fingerprint
    public string? BrowserFingerprint { get; set; }
    public string? CanvasFingerprint { get; set; }
    public string? WebGLFingerprint { get; set; }
    public string? AudioFingerprint { get; set; }
    public string? FontsHash { get; set; }
    public int? FontCount { get; set; }
    
    // Hardware Info
    public int? HardwareConcurrency { get; set; }
    public int? MaxTouchPoints { get; set; }
    public string? ScreenResolution { get; set; }
    public string? ColorDepth { get; set; }
    public string? PixelRatio { get; set; }
    public string? CPUFingerprint { get; set; }
    public string? WebGPUFingerprint { get; set; }
    public string? WebGPUVendor { get; set; }
    
    // Network Info
    public string? ConnectionType { get; set; }
    public string? EffectiveType { get; set; }
    public string? WebRTCLocalIPs { get; set; } // JSON array
    public string? WebRTCPublicIPs { get; set; } // JSON array
    
    // Timezone & Locale
    public string? Timezone { get; set; }
    public int? TimezoneOffset { get; set; }
    public string? Locale { get; set; }
    public string? Calendar { get; set; }
    
    // Browser Capabilities
    public bool? CookieEnabled { get; set; }
    public string? DoNotTrack { get; set; }
    public bool? LocalStorageAvailable { get; set; }
    public bool? SessionStorageAvailable { get; set; }
    public bool? IndexedDBAvailable { get; set; }
    
    // Protocol & Features
    public string? HTTPVersion { get; set; }
    public bool? HTTP2Support { get; set; }
    public bool? HTTP3Support { get; set; }
    public bool? ServiceWorkerActive { get; set; }
    public bool? WebAssemblySupport { get; set; }
    
    // Device Info
    public string? MediaDevicesHash { get; set; }
    public int? MediaDeviceCount { get; set; }
    public double? BatteryLevel { get; set; }
    public bool? BatteryCharging { get; set; }
    
    // Geolocation
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? LocationAccuracy { get; set; }
    
    // Permissions
    public string? PermissionsGranted { get; set; } // JSON array
    
    // Performance
    public double? MemoryUsed { get; set; }
    public double? MemoryLimit { get; set; }
    public double? PerformanceScore { get; set; }

    // Full tracking data as JSON
    public string TrackingDataJson { get; set; } = string.Empty;

    // CNSA 2.0 Cryptographic Hash
    public string SHA384Hash { get; set; } = string.Empty;

    // Inferred Region (from InferredRegionEngine)
    public string? InferredRegionId { get; set; }
    public double? InferredRegionConfidence { get; set; } // 0.0 to 1.0
    public string? InferredRegionFlagsJson { get; set; } // JSON with mismatch flags

    // Navigation properties
    public Visitor Visitor { get; set; } = null!;
    public VPNProxyDetection? VPNProxyDetection { get; set; }
}

public class VPNProxyDetection
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    
    // Detection Results
    public string RemoteIP { get; set; } = string.Empty;
    public string IPChain { get; set; } = string.Empty; // JSON array
    public string ProxyHeaders { get; set; } = string.Empty; // JSON object
    public string DetectionIndicators { get; set; } = string.Empty; // JSON array
    public string SuspicionLevel { get; set; } = string.Empty;
    public bool IsLikelyVPNOrProxy { get; set; }
    
    // Analysis Details
    public bool HasProxyHeaders { get; set; }
    public int IPHopCount { get; set; }
    public bool HasViaHeader { get; set; }
    public bool HasForwardedFor { get; set; }
    public int IndicatorCount { get; set; }
    
    // IP Classification
    public bool IsKnownVPNProvider { get; set; }
    public bool IsDatacenterIP { get; set; }
    public bool IsTorExitNode { get; set; }
    public bool IsPrivateIP { get; set; }
    public bool IsLocalhost { get; set; }
    public string IPType { get; set; } = string.Empty;
    
    // Geolocation (if available)
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? ISP { get; set; }
    public string? ASN { get; set; }
    
    public DateTime DetectedAt { get; set; }
    
    // Navigation property
    public Visit Visit { get; set; } = null!;
}

