using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ask2Ask.Services;

namespace Ask2Ask.Pages.Api;

/// <summary>
/// API Endpoint: GET /api/stats
/// Returns tracking statistics and analytics
/// Requires: read scope
/// </summary>
public class StatsModel : PageModel
{
    private readonly TrackingService _trackingService;
    private readonly ILogger<StatsModel> _logger;

    public StatsModel(TrackingService trackingService, ILogger<StatsModel> logger)
    {
        _trackingService = trackingService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var stats = await _trackingService.GetTrackingDashboardDataAsync();
            
            return new JsonResult(new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                data = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve stats");
            return new JsonResult(new
            {
                success = false,
                error = "Failed to retrieve statistics",
                timestamp = DateTime.UtcNow
            })
            {
                StatusCode = 500
            };
        }
    }
}

