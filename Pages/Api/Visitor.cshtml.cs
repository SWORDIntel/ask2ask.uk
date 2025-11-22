using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ask2Ask.Services;

namespace Ask2Ask.Pages.Api;

/// <summary>
/// API Endpoint: GET /api/visitor?hash=abc123...
/// Returns detailed visitor information including all visits
/// Requires: read scope
/// </summary>
public class VisitorModel : PageModel
{
    private readonly TrackingService _trackingService;
    private readonly ILogger<VisitorModel> _logger;

    public VisitorModel(TrackingService trackingService, ILogger<VisitorModel> logger)
    {
        _trackingService = trackingService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync([FromQuery] string? hash)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return new JsonResult(new
                {
                    success = false,
                    error = "Visitor hash required",
                    timestamp = DateTime.UtcNow
                })
                {
                    StatusCode = 400
                };
            }

            var visitor = await _trackingService.GetVisitorDetailsAsync(hash);
            
            if (visitor == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    error = "Visitor not found",
                    timestamp = DateTime.UtcNow
                })
                {
                    StatusCode = 404
                };
            }

            return new JsonResult(new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                data = visitor
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve visitor details");
            return new JsonResult(new
            {
                success = false,
                error = "Failed to retrieve visitor details",
                timestamp = DateTime.UtcNow
            })
            {
                StatusCode = 500
            };
        }
    }
}

