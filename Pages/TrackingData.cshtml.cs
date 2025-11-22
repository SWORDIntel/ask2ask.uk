using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ask2Ask.Services;
using Ask2Ask.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ask2Ask.Pages;

public class TrackingDataModel : PageModel
{
    private readonly TrackingDbContext _context;
    private readonly TrackingService _trackingService;

    public TrackingDataModel(TrackingDbContext context, TrackingService trackingService)
    {
        _context = context;
        _trackingService = trackingService;
    }

    /// <summary>
    /// Get all visitors
    /// GET /TrackingData?action=visitors
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        string? action = null,
        int? visitorId = null,
        int? visitId = null,
        int page = 1,
        int pageSize = 50)
    {
        try
        {
            return action?.ToLower() switch
            {
                "visitors" => await GetVisitorsAsync(page, pageSize),
                "visitor" when visitorId.HasValue => await GetVisitorDetailsAsync(visitorId.Value),
                "visits" => await GetVisitsAsync(page, pageSize),
                "visit" when visitId.HasValue => await GetVisitDetailsAsync(visitId.Value),
                "vpn" => await GetVPNDetectionsAsync(page, pageSize),
                "stats" => await GetStatsAsync(),
                "export" => await ExportAllDataAsync(),
                _ => new JsonResult(new
                {
                    error = "Invalid action",
                    availableActions = new[]
                    {
                        "visitors - List all visitors",
                        "visitor&visitorId=X - Get visitor details",
                        "visits - List all visits",
                        "visit&visitId=X - Get visit details",
                        "vpn - List VPN detections",
                        "stats - Get statistics",
                        "export - Export all data"
                    }
                })
            };
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private async Task<IActionResult> GetVisitorsAsync(int page, int pageSize)
    {
        var totalVisitors = await _context.Visitors.CountAsync();
        var visitors = await _context.Visitors
            .OrderByDescending(v => v.LastSeen)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new
            {
                v.Id,
                v.FingerprintHash,
                v.FirstSeen,
                v.LastSeen,
                v.VisitCount,
                v.UserAgent,
                v.Platform,
                v.Language
            })
            .ToListAsync();

        return new JsonResult(new
        {
            totalVisitors,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalVisitors / (double)pageSize),
            visitors
        });
    }

    private async Task<IActionResult> GetVisitorDetailsAsync(int visitorId)
    {
        var visitor = await _context.Visitors
            .Include(v => v.Visits)
                .ThenInclude(visit => visit.VPNProxyDetection)
            .FirstOrDefaultAsync(v => v.Id == visitorId);

        if (visitor == null)
        {
            return new JsonResult(new { error = "Visitor not found" }) { StatusCode = 404 };
        }

        var visitorSummary = await _trackingService.GetVisitorSummaryAsync(visitor.FingerprintHash);

        return new JsonResult(new
        {
            visitor = new
            {
                visitor.Id,
                visitor.FingerprintHash,
                visitor.FirstSeen,
                visitor.LastSeen,
                visitor.VisitCount,
                visitor.UserAgent,
                visitor.Platform,
                visitor.Language
            },
            summary = visitorSummary,
            visits = visitor.Visits.OrderByDescending(v => v.Timestamp).Select(v => new
            {
                v.Id,
                v.Timestamp,
                v.SessionId,
                v.RemoteIP,
                v.UserAgent,
                VPNDetected = v.VPNProxyDetection?.IsLikelyVPNOrProxy ?? false,
                SuspicionLevel = v.VPNProxyDetection?.SuspicionLevel ?? "Unknown"
            })
        });
    }

    private async Task<IActionResult> GetVisitsAsync(int page, int pageSize)
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
                v.VisitorId,
                v.Timestamp,
                v.SessionId,
                v.RemoteIP,
                v.ForwardedFor,
                v.UserAgent,
                v.Referer,
                VisitorFingerprint = v.Visitor.FingerprintHash,
                VisitorTotalVisits = v.Visitor.VisitCount,
                VPNDetected = v.VPNProxyDetection != null && v.VPNProxyDetection.IsLikelyVPNOrProxy,
                SuspicionLevel = v.VPNProxyDetection != null ? v.VPNProxyDetection.SuspicionLevel : "Unknown"
            })
            .ToListAsync();

        return new JsonResult(new
        {
            totalVisits,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalVisits / (double)pageSize),
            visits
        });
    }

    private async Task<IActionResult> GetVisitDetailsAsync(int visitId)
    {
        var standardizedOutput = await _trackingService.GetStandardizedOutputAsync(visitId);
        return new JsonResult(standardizedOutput);
    }

    private async Task<IActionResult> GetVPNDetectionsAsync(int page, int pageSize)
    {
        var totalDetections = await _context.VPNProxyDetections
            .Where(v => v.IsLikelyVPNOrProxy)
            .CountAsync();

        var detectionsRaw = await _context.VPNProxyDetections
            .Include(v => v.Visit)
                .ThenInclude(visit => visit.Visitor)
            .Where(v => v.IsLikelyVPNOrProxy)
            .OrderByDescending(v => v.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var detections = detectionsRaw.Select(v => new
        {
            v.Id,
            v.VisitId,
            VisitorId = v.Visit.VisitorId,
            v.DetectedAt,
            v.RemoteIP,
            IPChain = JsonSerializer.Deserialize<List<string>>(v.IPChain),
            v.SuspicionLevel,
            v.IndicatorCount,
            Indicators = JsonSerializer.Deserialize<List<string>>(v.DetectionIndicators),
            v.IsKnownVPNProvider,
            v.IsDatacenterIP,
            v.IsTorExitNode,
            v.IPType
        });

        return new JsonResult(new
        {
            totalDetections,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalDetections / (double)pageSize),
            detections
        });
    }

    private async Task<IActionResult> GetStatsAsync()
    {
        var totalVisitors = await _context.Visitors.CountAsync();
        var totalVisits = await _context.Visits.CountAsync();
        var vpnDetections = await _context.VPNProxyDetections
            .Where(v => v.IsLikelyVPNOrProxy)
            .CountAsync();

        var suspicionLevels = await _context.VPNProxyDetections
            .GroupBy(v => v.SuspicionLevel)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        var topVPNProviders = await _context.VPNProxyDetections
            .Where(v => v.IsKnownVPNProvider)
            .GroupBy(v => v.RemoteIP)
            .Select(g => new { IP = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var returningVisitors = await _context.Visitors
            .Where(v => v.VisitCount > 1)
            .CountAsync();

        return new JsonResult(new
        {
            overview = new
            {
                totalVisitors,
                totalVisits,
                vpnDetections,
                vpnDetectionRate = totalVisits > 0 ? (double)vpnDetections / totalVisits * 100 : 0,
                returningVisitors,
                returningVisitorRate = totalVisitors > 0 ? (double)returningVisitors / totalVisitors * 100 : 0
            },
            suspicionLevels,
            topVPNProviders,
            recentActivity = new
            {
                last24Hours = await _context.Visits
                    .Where(v => v.Timestamp >= DateTime.UtcNow.AddHours(-24))
                    .CountAsync(),
                last7Days = await _context.Visits
                    .Where(v => v.Timestamp >= DateTime.UtcNow.AddDays(-7))
                    .CountAsync(),
                last30Days = await _context.Visits
                    .Where(v => v.Timestamp >= DateTime.UtcNow.AddDays(-30))
                    .CountAsync()
            }
        });
    }

    private async Task<IActionResult> ExportAllDataAsync()
    {
        var visitors = await _context.Visitors
            .Include(v => v.Visits)
                .ThenInclude(visit => visit.VPNProxyDetection)
            .ToListAsync();

        var export = visitors.Select(v => new
        {
            Visitor = new
            {
                v.Id,
                v.FingerprintHash,
                v.FirstSeen,
                v.LastSeen,
                v.VisitCount,
                v.UserAgent,
                v.Platform,
                v.Language
            },
            Visits = v.Visits.Select(visit =>
            {
                var trackingData = JsonSerializer.Deserialize<JsonElement>(visit.TrackingDataJson);
                var vpnDetection = visit.VPNProxyDetection != null ? new
                {
                    visit.VPNProxyDetection.RemoteIP,
                    IPChain = JsonSerializer.Deserialize<List<string>>(visit.VPNProxyDetection.IPChain),
                    ProxyHeaders = JsonSerializer.Deserialize<JsonElement>(visit.VPNProxyDetection.ProxyHeaders),
                    Indicators = JsonSerializer.Deserialize<List<string>>(visit.VPNProxyDetection.DetectionIndicators),
                    visit.VPNProxyDetection.SuspicionLevel,
                    visit.VPNProxyDetection.IsLikelyVPNOrProxy,
                    visit.VPNProxyDetection.IsKnownVPNProvider,
                    visit.VPNProxyDetection.IsDatacenterIP,
                    visit.VPNProxyDetection.IsTorExitNode,
                    visit.VPNProxyDetection.IPType
                } : null;

                return new
                {
                    visit.Id,
                    visit.Timestamp,
                    visit.SessionId,
                    visit.RemoteIP,
                    visit.ForwardedFor,
                    visit.UserAgent,
                    visit.Referer,
                    visit.SHA384Hash,
                    TrackingData = trackingData,
                    VPNProxyDetection = vpnDetection
                };
            })
        });

        return new JsonResult(new
        {
            exportedAt = DateTime.UtcNow,
            totalVisitors = visitors.Count,
            totalVisits = visitors.Sum(v => v.Visits.Count),
            data = export
        });
    }
}

