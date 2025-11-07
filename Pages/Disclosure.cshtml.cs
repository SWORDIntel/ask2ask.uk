using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ask2Ask.Pages;

public class DisclosureModel : PageModel
{
    private readonly ILogger<DisclosureModel> _logger;

    public DisclosureModel(ILogger<DisclosureModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        // This page has NO tracking - it's just the disclosure/consent page
        _logger.LogInformation("Disclosure page viewed - no tracking active");
    }
}
