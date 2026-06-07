using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages.Reports;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IReportService _reportService;

    public string Tab { get; private set; } = "stock";

    public StockReportDto?        StockReport     { get; private set; }
    public CheckedOutReportDto?   CheckedOut      { get; private set; }
    public LostItemsReportDto?    LostItems       { get; private set; }
    public TakeInventoryReportDto? TakeInventory  { get; private set; }
    public IEnumerable<ActivityLogDto> ActivityLogs { get; private set; } = [];
    public IEnumerable<InventoryItemDto> BarcodeItems { get; private set; } = [];

    public DateTime? ActivityFrom { get; private set; }
    public DateTime? ActivityTo   { get; private set; }

    public IndexModel(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task OnGetAsync(string tab = "stock")
    {
        Tab = tab;
        switch (tab)
        {
            case "checkout":
                CheckedOut = await _reportService.GetCheckedOutReportAsync();
                break;
            case "lost":
                LostItems = await _reportService.GetLostItemsReportAsync();
                break;
            case "inventory":
                TakeInventory = await _reportService.GetTakeInventoryReportAsync();
                break;
            case "barcodes":
                StockReport = await _reportService.GetStockReportAsync();
                BarcodeItems = StockReport.AllItems.Where(i => !string.IsNullOrWhiteSpace(i.SKU));
                break;
            case "activity":
                if (DateTime.TryParse(Request.Query["from"], out var fromLocal))
                    ActivityFrom = fromLocal;
                if (DateTime.TryParse(Request.Query["to"], out var toLocal))
                    ActivityTo = toLocal;
                ActivityLogs = await _reportService.GetActivityReportAsync(
                    ActivityFrom?.ToUniversalTime(),
                    ActivityTo?.ToUniversalTime());
                break;
            default:
                StockReport = await _reportService.GetStockReportAsync();
                break;
        }
    }
}
