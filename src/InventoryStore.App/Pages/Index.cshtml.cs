using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IReportService _reportService;

    public DashboardSummaryDto Summary { get; private set; } = null!;
    public bool CanManage => User.IsInRole("Admin") || User.IsInRole("Manager");

    public IndexModel(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task OnGetAsync()
    {
        Summary = await _reportService.GetDashboardSummaryAsync();
    }
}
