using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ask2Ask.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ask2Ask.Pages.Api;

/// <summary>
/// API Endpoint: GET /api/export?format=ndjson&since=2025-01-01
/// Exports entire database for integration with Elasticsearch or other systems
/// Requires: export scope + client certificate
/// CNSA 2.0 compliant: All data includes SHA-384 hashes for integrity
/// </summary>
public class ExportModel : PageModel
{
    private readonly TrackingDbContext _context;
    private readonly ILogger<ExportModel> _logger;

    public ExportModel(TrackingDbContext context, ILogger<ExportModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] string format = "ndjson",
        [FromQuery] DateTime? since = null,
        [FromQuery] int? limit = null)
    {
        try
        {
            _logger.LogInformation($"Database export requested: format={format}, since={since}, limit={limit}");

            // Query all data
            var visitorsQuery = _context.Visitors
                .Include(v => v.Visits)
                    .ThenInclude(visit => visit.VPNProxyDetection)
                .AsQueryable();

            // Filter by date if specified
            if (since.HasValue)
            {
                visitorsQuery = visitorsQuery.Where(v => v.LastSeen >= since.Value);
            }

            // Apply limit if specified
            if (limit.HasValue)
            {
                visitorsQuery = visitorsQuery.Take(limit.Value);
            }

            var visitors = await visitorsQuery.ToListAsync();

            // Export based on format
            return format.ToLower() switch
            {
                "ndjson" => await ExportAsNDJson(visitors),
                "json" => ExportAsJson(visitors),
                "bulk" => await ExportAsElasticsearchBulk(visitors),
                _ => new JsonResult(new { success = false, error = "Unsupported format. Use: ndjson, json, or bulk" }) 
                    { StatusCode = 400 }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export database");
            return new JsonResult(new
            {
                success = false,
                error = "Failed to export database",
                timestamp = DateTime.UtcNow
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Export as newline-delimited JSON (NDJSON) - ideal for streaming
    /// </summary>
    private async Task<IActionResult> ExportAsNDJson(List<Visitor> visitors)
    {
        Response.ContentType = "application/x-ndjson";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=tracking-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ndjson");

        await Response.StartAsync();

        foreach (var visitor in visitors)
        {
            foreach (var visit in visitor.Visits)
            {
                var record = CreateExportRecord(visitor, visit);
                var json = JsonSerializer.Serialize(record);
                await Response.WriteAsync(json + "\n");
            }
        }

        await Response.CompleteAsync();
        return new EmptyResult();
    }

    /// <summary>
    /// Export as standard JSON array
    /// </summary>
    private IActionResult ExportAsJson(List<Visitor> visitors)
    {
        var records = new List<object>();

        foreach (var visitor in visitors)
        {
            foreach (var visit in visitor.Visits)
            {
                records.Add(CreateExportRecord(visitor, visit));
            }
        }

        return new JsonResult(new
        {
            success = true,
            timestamp = DateTime.UtcNow,
            count = records.Count,
            data = records
        });
    }

    /// <summary>
    /// Export as Elasticsearch Bulk API format
    /// </summary>
    private async Task<IActionResult> ExportAsElasticsearchBulk(List<Visitor> visitors)
    {
        Response.ContentType = "application/x-ndjson";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=tracking-bulk-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ndjson");

        await Response.StartAsync();

        var indexName = "tracking-visits";

        foreach (var visitor in visitors)
        {
            foreach (var visit in visitor.Visits)
            {
                var record = CreateExportRecord(visitor, visit);
                
                // Elasticsearch bulk format: action line + document line
                var action = new { index = new { _index = indexName, _id = $"visit-{visit.Id}" } };
                await Response.WriteAsync(JsonSerializer.Serialize(action) + "\n");
                await Response.WriteAsync(JsonSerializer.Serialize(record) + "\n");
            }
        }

        await Response.CompleteAsync();
        return new EmptyResult();
    }

    /// <summary>
    /// Create a comprehensive export record with all tracking data
    /// CNSA 2.0: Includes SHA-384 hashes for data integrity
    /// </summary>
    private object CreateExportRecord(Visitor visitor, Visit visit)
    {
        return new
        {
            // Record metadata
            exportTimestamp = DateTime.UtcNow,
            recordType = "visit",
            
            // CNSA 2.0 Cryptographic integrity
            dataHash = visit.SHA384Hash,
            
            // Visitor information
            visitor = new
            {
                id = visitor.Id,
                fingerprintHash = visitor.FingerprintHash,
                firstSeen = visitor.FirstSeen,
                lastSeen = visitor.LastSeen,
                visitCount = visitor.VisitCount,
                userAgent = visitor.UserAgent,
                platform = visitor.Platform,
                language = visitor.Language
            },
            
            // Visit information
            visit = new
            {
                id = visit.Id,
                timestamp = visit.Timestamp,
                sessionId = visit.SessionId,
                
                // Network
                network = new
                {
                    remoteIP = visit.RemoteIP,
                    forwardedFor = visit.ForwardedFor,
                    realIP = visit.RealIP,
                    connectionType = visit.ConnectionType,
                    effectiveType = visit.EffectiveType,
                    webRTCLocalIPs = visit.WebRTCLocalIPs,
                    webRTCPublicIPs = visit.WebRTCPublicIPs,
                    httpVersion = visit.HTTPVersion,
                    http2Support = visit.HTTP2Support,
                    http3Support = visit.HTTP3Support
                },
                
                // Fingerprints
                fingerprints = new
                {
                    canvas = visit.CanvasFingerprint,
                    webgl = visit.WebGLFingerprint,
                    audio = visit.AudioFingerprint,
                    cpu = visit.CPUFingerprint,
                    webgpu = visit.WebGPUFingerprint,
                    webgpuVendor = visit.WebGPUVendor,
                    fonts = visit.FontsHash,
                    fontCount = visit.FontCount,
                    mediaDevices = visit.MediaDevicesHash,
                    mediaDeviceCount = visit.MediaDeviceCount
                },
                
                // Hardware
                hardware = new
                {
                    hardwareConcurrency = visit.HardwareConcurrency,
                    maxTouchPoints = visit.MaxTouchPoints,
                    screenResolution = visit.ScreenResolution,
                    colorDepth = visit.ColorDepth,
                    pixelRatio = visit.PixelRatio,
                    batteryLevel = visit.BatteryLevel,
                    batteryCharging = visit.BatteryCharging,
                    memoryUsed = visit.MemoryUsed,
                    memoryLimit = visit.MemoryLimit,
                    performanceScore = visit.PerformanceScore
                },
                
                // Timezone & Locale
                locale = new
                {
                    timezone = visit.Timezone,
                    timezoneOffset = visit.TimezoneOffset,
                    locale = visit.Locale,
                    calendar = visit.Calendar
                },
                
                // Browser
                browser = new
                {
                    userAgent = visit.UserAgent,
                    referer = visit.Referer,
                    cookieEnabled = visit.CookieEnabled,
                    doNotTrack = visit.DoNotTrack,
                    localStorageAvailable = visit.LocalStorageAvailable,
                    sessionStorageAvailable = visit.SessionStorageAvailable,
                    indexedDBAvailable = visit.IndexedDBAvailable,
                    serviceWorkerActive = visit.ServiceWorkerActive,
                    webAssemblySupport = visit.WebAssemblySupport
                },
                
                // Geolocation
                geolocation = new
                {
                    latitude = visit.Latitude,
                    longitude = visit.Longitude,
                    accuracy = visit.LocationAccuracy
                },
                
                // Permissions
                permissions = visit.PermissionsGranted,
                
                // Full tracking data (all 130+ fields)
                fullTrackingData = visit.TrackingDataJson
            },
            
            // VPN/Proxy Detection
            vpnProxy = visit.VPNProxyDetection != null ? new
            {
                isLikelyVPNOrProxy = visit.VPNProxyDetection.IsLikelyVPNOrProxy,
                suspicionLevel = visit.VPNProxyDetection.SuspicionLevel,
                remoteIP = visit.VPNProxyDetection.RemoteIP,
                ipChain = visit.VPNProxyDetection.IPChain,
                proxyHeaders = visit.VPNProxyDetection.ProxyHeaders,
                detectionIndicators = visit.VPNProxyDetection.DetectionIndicators,
                indicatorCount = visit.VPNProxyDetection.IndicatorCount,
                hasProxyHeaders = visit.VPNProxyDetection.HasProxyHeaders,
                ipHopCount = visit.VPNProxyDetection.IPHopCount,
                hasViaHeader = visit.VPNProxyDetection.HasViaHeader,
                hasForwardedFor = visit.VPNProxyDetection.HasForwardedFor,
                isKnownVPNProvider = visit.VPNProxyDetection.IsKnownVPNProvider,
                isDatacenterIP = visit.VPNProxyDetection.IsDatacenterIP,
                isTorExitNode = visit.VPNProxyDetection.IsTorExitNode,
                isPrivateIP = visit.VPNProxyDetection.IsPrivateIP,
                isLocalhost = visit.VPNProxyDetection.IsLocalhost,
                ipType = visit.VPNProxyDetection.IPType
            } : null
        };
    }
}

