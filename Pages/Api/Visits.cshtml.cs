using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ask2Ask.Services;

namespace Ask2Ask.Pages.Api;

/// <summary>
/// API Endpoint: GET /api/visits?page=1&pageSize=50
/// Returns paginated visit records
/// Requires: read scope
/// </summary>
public class VisitsModel : PageModel
{
    private readonly TrackingService _trackingService;
    private readonly ILogger<VisitsModel> _logger;

    public VisitsModel(TrackingService trackingService, ILogger<VisitsModel> logger)
    {
        _trackingService = trackingService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            // Limit page size to prevent abuse
            pageSize = Math.Min(pageSize, 1000);
            page = Math.Max(page, 1);

            var visits = await _trackingService.GetAllVisitsAsync(page, pageSize);
            
            return new JsonResult(new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                data = visits
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve visits");
            return new JsonResult(new
            {
                success = false,
                error = "Failed to retrieve visits",
                timestamp = DateTime.UtcNow
            })
            {
                StatusCode = 500
            };
        }
    }
}

