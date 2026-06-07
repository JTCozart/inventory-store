using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IReportService _reportService;

    public DashboardSummaryDto Summary { get; private set; } = null!;

    public IndexModel(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task OnGetAsync()
    {
        Summary = await _reportService.GetDashboardSummaryAsync();
    }
}
