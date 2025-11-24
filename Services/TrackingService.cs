using Ask2Ask.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ask2Ask.Services;

public class TrackingService
{
    private readonly TrackingDbContext _context;
    private readonly ILogger<TrackingService> _logger;
    private readonly IInferredRegionEngine _inferredRegionEngine;

    public TrackingService(
        TrackingDbContext context,
        ILogger<TrackingService> logger,
        IInferredRegionEngine inferredRegionEngine)
    {
        _context = context;
        _logger = logger;
        _inferredRegionEngine = inferredRegionEngine;
    }

    public async Task<Visit> RecordVisitAsync(
        string fingerprintHash,
        string sessionId,
        string? remoteIP,
        string? forwardedFor,
        string? userAgent,
        string? referer,
        object trackingData,
        object vpnProxyDetection)
    {
        // Find or create visitor
        var visitor = await _context.Visitors
            .FirstOrDefaultAsync(v => v.FingerprintHash == fingerprintHash);

        if (visitor == null)
        {
            visitor = new Visitor
            {
                FingerprintHash = fingerprintHash,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                VisitCount = 0,
                UserAgent = userAgent,
                Platform = ExtractPlatform(userAgent),
                Language = ExtractLanguage(trackingData)
            };
            _context.Visitors.Add(visitor);
        }
        else
        {
            visitor.LastSeen = DateTime.UtcNow;
            visitor.VisitCount++;
        }

        // Create visit record
        var trackingJson = JsonSerializer.Serialize(trackingData, new JsonSerializerOptions { WriteIndented = true });
        var visit = new Visit
        {
            Visitor = visitor,
            Timestamp = DateTime.UtcNow,
            SessionId = sessionId,
            RemoteIP = remoteIP,
            ForwardedFor = forwardedFor,
            UserAgent = userAgent,
            Referer = referer,
            TrackingDataJson = trackingJson,
            SHA384Hash = GenerateSHA384Hash(trackingJson)
        };

        // Extract additional fields from tracking data
        ExtractTrackingFields(visit, trackingData);

        _context.Visits.Add(visit);

        // Create VPN/Proxy detection record
        var vpnDetection = CreateVPNProxyDetection(visit, vpnProxyDetection);
        _context.VPNProxyDetections.Add(vpnDetection);

        await _context.SaveChangesAsync();

        // Run inferred region inference
        try
        {
            var correlation = await GetLatestAsnPingCorrelationForVisitorAsync(visitor.Id);
            var inferredRegion = await _inferredRegionEngine.InferAsync(visit, correlation);

            if (inferredRegion is not null)
            {
                visit.InferredRegionId = inferredRegion.RegionId;
                visit.InferredRegionConfidence = inferredRegion.Confidence;
                visit.InferredRegionFlagsJson = JsonSerializer.Serialize(inferredRegion.Flags);

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error running inferred region inference for visit {VisitId}", visit.Id);
            // Don't fail the visit recording if inference fails
        }

        return visit;
    }

    public async Task<VisitorSummary> GetVisitorSummaryAsync(string fingerprintHash)
    {
        var visitor = await _context.Visitors
            .Include(v => v.Visits)
                .ThenInclude(visit => visit.VPNProxyDetection)
            .FirstOrDefaultAsync(v => v.FingerprintHash == fingerprintHash);

        if (visitor == null)
        {
            return new VisitorSummary
            {
                IsNewVisitor = true,
                TotalVisits = 0
            };
        }

        var visits = visitor.Visits.OrderByDescending(v => v.Timestamp).ToList();
        var vpnDetections = visits
            .Select(v => v.VPNProxyDetection)
            .Where(d => d != null)
            .ToList();

        return new VisitorSummary
        {
            IsNewVisitor = false,
            VisitorId = visitor.Id,
            FingerprintHash = visitor.FingerprintHash,
            FirstSeen = visitor.FirstSeen,
            LastSeen = visitor.LastSeen,
            TotalVisits = visitor.VisitCount,
            RecentVisits = visits.Take(10).Select(v => new VisitSummary
            {
                VisitId = v.Id,
                Timestamp = v.Timestamp,
                RemoteIP = v.RemoteIP,
                IsLikelyVPNOrProxy = v.VPNProxyDetection?.IsLikelyVPNOrProxy ?? false,
                SuspicionLevel = v.VPNProxyDetection?.SuspicionLevel ?? "Unknown"
            }).ToList(),
            VPNProxyHistory = new VPNProxyHistory
            {
                TotalDetections = vpnDetections.Count,
                VPNDetectedCount = vpnDetections.Count(d => d!.IsLikelyVPNOrProxy),
                UniqueIPs = visits.Select(v => v.RemoteIP).Distinct().Count(),
                MostCommonSuspicionLevel = vpnDetections
                    .GroupBy(d => d!.SuspicionLevel)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "None"
            }
        };
    }

    public async Task<StandardizedTrackingOutput> GetStandardizedOutputAsync(int visitId)
    {
        var visit = await _context.Visits
            .Include(v => v.Visitor)
            .Include(v => v.VPNProxyDetection)
            .FirstOrDefaultAsync(v => v.Id == visitId);

        if (visit == null)
        {
            throw new InvalidOperationException($"Visit {visitId} not found");
        }

        return new StandardizedTrackingOutput
        {
            VisitId = visit.Id,
            VisitorId = visit.VisitorId,
            Timestamp = visit.Timestamp,
            SessionId = visit.SessionId,
            
            Identity = new IdentityInfo
            {
                FingerprintHash = visit.Visitor.FingerprintHash,
                IsReturningVisitor = visit.Visitor.VisitCount > 1,
                TotalVisits = visit.Visitor.VisitCount,
                FirstSeen = visit.Visitor.FirstSeen,
                LastSeen = visit.Visitor.LastSeen
            },
            
            Network = new NetworkInfo
            {
                RemoteIP = visit.RemoteIP,
                ForwardedFor = visit.ForwardedFor,
                IPChain = visit.VPNProxyDetection != null 
                    ? JsonSerializer.Deserialize<List<string>>(visit.VPNProxyDetection.IPChain) 
                    : new List<string>()
            },
            
            VPNProxy = visit.VPNProxyDetection != null ? new VPNProxyInfo
            {
                IsDetected = visit.VPNProxyDetection.IsLikelyVPNOrProxy,
                SuspicionLevel = visit.VPNProxyDetection.SuspicionLevel,
                IndicatorCount = visit.VPNProxyDetection.IndicatorCount,
                Indicators = JsonSerializer.Deserialize<List<string>>(visit.VPNProxyDetection.DetectionIndicators) ?? new List<string>(),
                IsKnownVPNProvider = visit.VPNProxyDetection.IsKnownVPNProvider,
                IsDatacenterIP = visit.VPNProxyDetection.IsDatacenterIP,
                IsTorExitNode = visit.VPNProxyDetection.IsTorExitNode,
                IPType = visit.VPNProxyDetection.IPType
            } : new VPNProxyInfo(),
            
            Browser = new BrowserInfo
            {
                UserAgent = visit.UserAgent,
                Platform = visit.Visitor.Platform,
                Language = visit.Visitor.Language
            },
            
            Security = new SecurityInfo
            {
                SHA384Hash = visit.SHA384Hash,
                CNSA2_0_Compliant = true
            },
            
            RawData = JsonSerializer.Deserialize<JsonElement>(visit.TrackingDataJson)
        };
    }

    private VPNProxyDetection CreateVPNProxyDetection(Visit visit, object vpnProxyData)
    {
        var json = JsonSerializer.Serialize(vpnProxyData);
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        return new VPNProxyDetection
        {
            Visit = visit,
            RemoteIP = data.GetProperty("RemoteIP").GetString() ?? "",
            IPChain = JsonSerializer.Serialize(data.GetProperty("IPChain")),
            ProxyHeaders = data.TryGetProperty("ProxyHeaders", out var headers) 
                ? JsonSerializer.Serialize(headers) 
                : "{}",
            DetectionIndicators = JsonSerializer.Serialize(data.GetProperty("DetectionIndicators")),
            SuspicionLevel = data.GetProperty("SuspicionLevel").GetString() ?? "Unknown",
            IsLikelyVPNOrProxy = data.GetProperty("IsLikelyVPNOrProxy").GetBoolean(),
            HasProxyHeaders = data.GetProperty("Analysis").GetProperty("HasProxyHeaders").GetBoolean(),
            IPHopCount = data.GetProperty("Analysis").GetProperty("IPHopCount").GetInt32(),
            HasViaHeader = data.GetProperty("Analysis").GetProperty("HasViaHeader").GetBoolean(),
            HasForwardedFor = data.GetProperty("Analysis").GetProperty("HasForwardedFor").GetBoolean(),
            IndicatorCount = data.GetProperty("Analysis").GetProperty("IndicatorCount").GetInt32(),
            IsKnownVPNProvider = data.GetProperty("DetectionIndicators").EnumerateArray()
                .Any(i => i.GetString()?.Contains("known VPN provider") ?? false),
            IsDatacenterIP = data.GetProperty("DetectionIndicators").EnumerateArray()
                .Any(i => i.GetString()?.Contains("datacenter") ?? false),
            IsTorExitNode = data.GetProperty("DetectionIndicators").EnumerateArray()
                .Any(i => i.GetString()?.Contains("Tor exit node") ?? false),
            IsPrivateIP = false, // Extract from IPGeolocation if available
            IsLocalhost = false,
            IPType = "IPv4 Public", // Extract from IPGeolocation if available
            DetectedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Get tracking dashboard data for API
    /// </summary>
    public async Task<object> GetTrackingDashboardDataAsync()
    {
        var totalVisitors = await _context.Visitors.CountAsync();
        var totalVisits = await _context.Visits.CountAsync();
        var vpnDetections = await _context.VPNProxyDetections.CountAsync(d => d.IsLikelyVPNOrProxy);
        var returningVisitors = await _context.Visitors.CountAsync(v => v.VisitCount > 1);

        var suspicionLevels = await _context.VPNProxyDetections
            .GroupBy(d => d.SuspicionLevel)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var recentActivity24Hours = await _context.Visits.CountAsync(v => v.Timestamp >= DateTime.UtcNow.AddHours(-24));
        var recentActivity7Days = await _context.Visits.CountAsync(v => v.Timestamp >= DateTime.UtcNow.AddDays(-7));
        var recentActivity30Days = await _context.Visits.CountAsync(v => v.Timestamp >= DateTime.UtcNow.AddDays(-30));

        return new
        {
            Overview = new
            {
                TotalVisitors = totalVisitors,
                TotalVisits = totalVisits,
                VpnDetections = vpnDetections,
                VpnDetectionRate = totalVisits > 0 ? (double)vpnDetections / totalVisits : 0,
                ReturningVisitors = returningVisitors,
                ReturningVisitorRate = totalVisitors > 0 ? (double)returningVisitors / totalVisitors : 0
            },
            SuspicionLevels = suspicionLevels,
            RecentActivity = new
            {
                Last24Hours = recentActivity24Hours,
                Last7Days = recentActivity7Days,
                Last30Days = recentActivity30Days
            }
        };
    }

    /// <summary>
    /// Get all visits with pagination for API
    /// </summary>
    public async Task<object> GetAllVisitsAsync(int page = 1, int pageSize = 50)
    {
        var totalVisits = await _context.Visits.CountAsync();
        var visits = await _context.Visits
            .Include(v => v.Visitor)
            .Include(v => v.VPNProxyDetection)
            .OrderByDescending(v => v.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new
            {
                v.Id,
                VisitorId = v.Visitor.Id,
                VisitorHash = v.Visitor.FingerprintHash,
                v.Timestamp,
                v.RemoteIP,
                v.UserAgent,
                v.SHA384Hash,
                VpnDetection = v.VPNProxyDetection != null ? new
                {
                    v.VPNProxyDetection.RemoteIP,
                    v.VPNProxyDetection.SuspicionLevel,
                    v.VPNProxyDetection.IsLikelyVPNOrProxy
                } : null,
                InferredRegion = v.InferredRegionId != null ? new
                {
                    v.InferredRegionId,
                    Confidence = v.InferredRegionConfidence ?? 0,
                    Flags = v.InferredRegionFlagsJson
                } : null
            })
            .ToListAsync();

        return new
        {
            TotalVisits = totalVisits,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalVisits / pageSize),
            Visits = visits
        };
    }

    /// <summary>
    /// Get visitor details for API
    /// </summary>
    public async Task<object?> GetVisitorDetailsAsync(string fingerprintHash)
    {
        var visitor = await _context.Visitors
            .Include(v => v.Visits)
                .ThenInclude(visit => visit.VPNProxyDetection)
            .FirstOrDefaultAsync(v => v.FingerprintHash == fingerprintHash);

        if (visitor == null)
        {
            return null;
        }

        var visits = visitor.Visits.Select(v => new
        {
            v.Id,
            v.Timestamp,
            v.RemoteIP,
            v.UserAgent,
            v.SHA384Hash,
            VpnDetection = v.VPNProxyDetection != null ? new
            {
                v.VPNProxyDetection.RemoteIP,
                v.VPNProxyDetection.IPChain,
                v.VPNProxyDetection.SuspicionLevel,
                v.VPNProxyDetection.IsLikelyVPNOrProxy,
                v.VPNProxyDetection.DetectionIndicators
            } : null,
            InferredRegion = v.InferredRegionId != null ? new
            {
                v.InferredRegionId,
                Confidence = v.InferredRegionConfidence ?? 0,
                Flags = v.InferredRegionFlagsJson
            } : null
        }).OrderByDescending(v => v.Timestamp);

        // Calculate summary of inferred regions for this visitor
        var regionSummary = visitor.Visits
            .Where(v => !string.IsNullOrEmpty(v.InferredRegionId))
            .GroupBy(v => v.InferredRegionId)
            .Select(g => new
            {
                RegionId = g.Key,
                Count = g.Count(),
                AverageConfidence = g.Average(v => v.InferredRegionConfidence ?? 0)
            })
            .OrderByDescending(r => r.Count)
            .ToList();

        return new
        {
            visitor.Id,
            visitor.FingerprintHash,
            visitor.FirstSeen,
            visitor.LastSeen,
            visitor.VisitCount,
            visitor.UserAgent,
            visitor.Platform,
            visitor.Language,
            RegionSummary = regionSummary,
            Visits = visits
        };
    }

    private void ExtractTrackingFields(Visit visit, object trackingData)
    {
        try
        {
            var json = JsonSerializer.Serialize(trackingData);
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // Basic Info
            if (data.TryGetProperty("basicInfo", out var basicInfo))
            {
                visit.HardwareConcurrency = basicInfo.TryGetProperty("hardwareConcurrency", out var hc) 
                    ? hc.GetInt32() : null;
                visit.MaxTouchPoints = basicInfo.TryGetProperty("maxTouchPoints", out var mtp) 
                    ? mtp.GetInt32() : null;
                visit.CookieEnabled = basicInfo.TryGetProperty("cookieEnabled", out var ce) 
                    ? ce.GetBoolean() : (bool?)null;
                visit.DoNotTrack = basicInfo.TryGetProperty("doNotTrack", out var dnt) 
                    ? dnt.GetString() : null;
                
                // Timezone
                if (basicInfo.TryGetProperty("timezone", out var tz))
                {
                    visit.Timezone = tz.TryGetProperty("timezone", out var tzName) 
                        ? tzName.GetString() : null;
                    visit.TimezoneOffset = tz.TryGetProperty("timezoneOffset", out var tzOffset) 
                        ? tzOffset.GetInt32() : (int?)null;
                    visit.Locale = tz.TryGetProperty("locale", out var locale) 
                        ? locale.GetString() : null;
                    visit.Calendar = tz.TryGetProperty("calendar", out var calendar) 
                        ? calendar.GetString() : null;
                }
            }

            // Fingerprints
            if (data.TryGetProperty("fingerprints", out var fingerprints))
            {
                visit.CanvasFingerprint = fingerprints.TryGetProperty("canvas", out var canvas) 
                    ? canvas.GetString() : null;
                visit.WebGLFingerprint = fingerprints.TryGetProperty("webgl", out var webgl) 
                    ? webgl.GetString() : null;
                visit.AudioFingerprint = fingerprints.TryGetProperty("audio", out var audio) 
                    ? audio.GetString() : null;
                visit.FontsHash = fingerprints.TryGetProperty("fontsHash", out var fontsHash) 
                    ? fontsHash.GetString() : null;
                visit.FontCount = fingerprints.TryGetProperty("fontCount", out var fontCount) 
                    ? fontCount.GetInt32() : (int?)null;
                visit.CPUFingerprint = fingerprints.TryGetProperty("cpuFingerprint", out var cpuFp) 
                    ? cpuFp.GetString() : null;
                visit.WebGPUFingerprint = fingerprints.TryGetProperty("webgpuFingerprint", out var gpuFp) 
                    ? gpuFp.GetString() : null;
                visit.WebGPUVendor = fingerprints.TryGetProperty("webgpuVendor", out var gpuVendor) 
                    ? gpuVendor.GetString() : null;
            }

            // Hardware
            if (data.TryGetProperty("hardware", out var hardware))
            {
                if (hardware.TryGetProperty("screen", out var screen))
                {
                    var width = screen.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                    var height = screen.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                    visit.ScreenResolution = $"{width}x{height}";
                    visit.ColorDepth = screen.TryGetProperty("colorDepth", out var cd) 
                        ? cd.GetInt32().ToString() : null;
                    visit.PixelRatio = screen.TryGetProperty("devicePixelRatio", out var pr) 
                        ? pr.GetDouble().ToString() : null;
                }
                
                // Battery
                if (hardware.TryGetProperty("battery", out var battery))
                {
                    visit.BatteryLevel = battery.TryGetProperty("level", out var level) 
                        ? level.GetDouble() : (double?)null;
                    visit.BatteryCharging = battery.TryGetProperty("charging", out var charging) 
                        ? charging.GetBoolean() : (bool?)null;
                }
                
                // Media Devices
                if (hardware.TryGetProperty("mediaDevices", out var mediaDevices))
                {
                    visit.MediaDevicesHash = mediaDevices.TryGetProperty("hash", out var mdHash) 
                        ? mdHash.GetString() : null;
                    visit.MediaDeviceCount = mediaDevices.TryGetProperty("count", out var mdCount) 
                        ? mdCount.GetInt32() : (int?)null;
                }
            }

            // Network
            if (data.TryGetProperty("network", out var network))
            {
                visit.ConnectionType = network.TryGetProperty("type", out var connType) 
                    ? connType.GetString() : null;
                visit.EffectiveType = network.TryGetProperty("effectiveType", out var effType) 
                    ? effType.GetString() : null;
                
                // WebRTC IPs
                if (network.TryGetProperty("webrtc", out var webrtc))
                {
                    if (webrtc.TryGetProperty("localIPs", out var localIPs))
                        visit.WebRTCLocalIPs = JsonSerializer.Serialize(localIPs);
                    if (webrtc.TryGetProperty("publicIPs", out var publicIPs))
                        visit.WebRTCPublicIPs = JsonSerializer.Serialize(publicIPs);
                }
                
                // HTTP Version
                if (network.TryGetProperty("httpVersion", out var httpVer))
                {
                    visit.HTTPVersion = httpVer.GetString();
                }
                if (network.TryGetProperty("http2Support", out var http2))
                {
                    visit.HTTP2Support = http2.GetBoolean();
                }
                if (network.TryGetProperty("http3Support", out var http3))
                {
                    visit.HTTP3Support = http3.GetBoolean();
                }
            }

            // Storage
            if (data.TryGetProperty("storage", out var storage))
            {
                visit.LocalStorageAvailable = storage.TryGetProperty("localStorage", out var ls) 
                    ? ls.GetBoolean() : (bool?)null;
                visit.SessionStorageAvailable = storage.TryGetProperty("sessionStorage", out var ss) 
                    ? ss.GetBoolean() : (bool?)null;
                visit.IndexedDBAvailable = storage.TryGetProperty("indexedDB", out var idb) 
                    ? idb.GetBoolean() : (bool?)null;
            }

            // Performance
            if (data.TryGetProperty("performance", out var performance))
            {
                if (performance.TryGetProperty("memory", out var memory))
                {
                    visit.MemoryUsed = memory.TryGetProperty("usedJSHeapSize", out var used) 
                        ? used.GetDouble() : (double?)null;
                    visit.MemoryLimit = memory.TryGetProperty("jsHeapSizeLimit", out var limit) 
                        ? limit.GetDouble() : (double?)null;
                }
                visit.PerformanceScore = performance.TryGetProperty("score", out var score) 
                    ? score.GetDouble() : (double?)null;
            }

            // Permissions
            if (data.TryGetProperty("permissions", out var permissions))
            {
                visit.PermissionsGranted = JsonSerializer.Serialize(permissions);
            }

            // Geolocation
            if (data.TryGetProperty("geolocation", out var geo))
            {
                if (geo.TryGetProperty("coords", out var coords))
                {
                    visit.Latitude = coords.TryGetProperty("latitude", out var lat) 
                        ? lat.GetDouble() : (double?)null;
                    visit.Longitude = coords.TryGetProperty("longitude", out var lon) 
                        ? lon.GetDouble() : (double?)null;
                    visit.LocationAccuracy = coords.TryGetProperty("accuracy", out var acc) 
                        ? acc.GetDouble() : (double?)null;
                }
            }

            // Browser Features
            if (data.TryGetProperty("features", out var features))
            {
                visit.ServiceWorkerActive = features.TryGetProperty("serviceWorker", out var sw) 
                    ? sw.GetBoolean() : (bool?)null;
                visit.WebAssemblySupport = features.TryGetProperty("webAssembly", out var wasm) 
                    ? wasm.GetBoolean() : (bool?)null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract tracking fields");
        }
    }

    private string? ExtractPlatform(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return null;
        
        if (userAgent.Contains("Windows")) return "Windows";
        if (userAgent.Contains("Mac")) return "macOS";
        if (userAgent.Contains("Linux")) return "Linux";
        if (userAgent.Contains("Android")) return "Android";
        if (userAgent.Contains("iOS") || userAgent.Contains("iPhone")) return "iOS";
        
        return "Unknown";
    }

    private string? ExtractLanguage(object trackingData)
    {
        try
        {
            var json = JsonSerializer.Serialize(trackingData);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (data.TryGetProperty("basicInfo", out var basicInfo) &&
                basicInfo.TryGetProperty("language", out var lang))
            {
                return lang.GetString();
            }
        }
        catch { }
        
        return null;
    }

    private async Task<AsnPingCorrelation?> GetLatestAsnPingCorrelationForVisitorAsync(int visitorId)
    {
        return await _context.AsnPingCorrelations
            .Where(c => c.VisitorId == visitorId)
            .OrderByDescending(c => c.LastSeen)
            .FirstOrDefaultAsync();
    }

    private string GenerateSHA384Hash(string data)
    {
        using var sha384 = SHA384.Create();
        var hashBytes = sha384.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }
}

// DTOs for standardized output
public class VisitorSummary
{
    public bool IsNewVisitor { get; set; }
    public int VisitorId { get; set; }
    public string FingerprintHash { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int TotalVisits { get; set; }
    public List<VisitSummary> RecentVisits { get; set; } = new();
    public VPNProxyHistory VPNProxyHistory { get; set; } = new();
}

public class VisitSummary
{
    public int VisitId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? RemoteIP { get; set; }
    public bool IsLikelyVPNOrProxy { get; set; }
    public string SuspicionLevel { get; set; } = string.Empty;
}

public class VPNProxyHistory
{
    public int TotalDetections { get; set; }
    public int VPNDetectedCount { get; set; }
    public int UniqueIPs { get; set; }
    public string MostCommonSuspicionLevel { get; set; } = string.Empty;
}

public class StandardizedTrackingOutput
{
    public int VisitId { get; set; }
    public int VisitorId { get; set; }
    public DateTime Timestamp { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public IdentityInfo Identity { get; set; } = new();
    public NetworkInfo Network { get; set; } = new();
    public VPNProxyInfo VPNProxy { get; set; } = new();
    public BrowserInfo Browser { get; set; } = new();
    public SecurityInfo Security { get; set; } = new();
    public JsonElement RawData { get; set; }
}

public class IdentityInfo
{
    public string FingerprintHash { get; set; } = string.Empty;
    public bool IsReturningVisitor { get; set; }
    public int TotalVisits { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}

public class NetworkInfo
{
    public string? RemoteIP { get; set; }
    public string? ForwardedFor { get; set; }
    public List<string> IPChain { get; set; } = new();
}

public class VPNProxyInfo
{
    public bool IsDetected { get; set; }
    public string SuspicionLevel { get; set; } = "None";
    public int IndicatorCount { get; set; }
    public List<string> Indicators { get; set; } = new();
    public bool IsKnownVPNProvider { get; set; }
    public bool IsDatacenterIP { get; set; }
    public bool IsTorExitNode { get; set; }
    public string IPType { get; set; } = string.Empty;
}

public class BrowserInfo
{
    public string? UserAgent { get; set; }
    public string? Platform { get; set; }
    public string? Language { get; set; }
}

public class SecurityInfo
{
    public string SHA384Hash { get; set; } = string.Empty;
    public bool CNSA2_0_Compliant { get; set; }
}

