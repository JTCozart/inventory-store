using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Reports;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IReportService _reportService;
    private readonly IInventoryService _inventoryService;
    private readonly InventoryStore.App.Modules.IModuleRegistry _modules;

    public string Tab { get; private set; } = "stock";
    public bool CostEnabled { get; private set; }
    public bool ForecastEnabled { get; private set; }

    public StockReportDto?        StockReport     { get; private set; }
    public CheckedOutReportDto?   CheckedOut      { get; private set; }
    public LostItemsReportDto?    LostItems       { get; private set; }
    public TakeInventoryReportDto? TakeInventory  { get; private set; }
    public IEnumerable<ActivityLogDto> ActivityLogs { get; private set; } = [];
    public IEnumerable<InventoryItemDto> BarcodeItems { get; private set; } = [];

    public DateTime? ActivityFrom { get; private set; }
    public DateTime? ActivityTo   { get; private set; }

    public IndexModel(IReportService reportService, IInventoryService inventoryService,
        InventoryStore.App.Modules.IModuleRegistry modules)
    {
        _reportService    = reportService;
        _inventoryService = inventoryService;
        _modules          = modules;
    }

    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        var csv = await _inventoryService.ExportToCsvAsync();
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var filename = $"inventory-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(bytes, "text/csv", filename);
    }

    public IEnumerable<InventoryItemDto> ExpiredItems  { get; private set; } = [];
    public IEnumerable<InventoryItemDto> ExpiringItems { get; private set; } = [];

    public async Task OnGetAsync(string tab = "stock")
    {
        Tab = tab;
        CostEnabled     = await _modules.IsEnabledAsync("cost");
        ForecastEnabled = await _modules.IsEnabledAsync("forecast");
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
            case "expiry":
                var allItems  = await _inventoryService.GetAllItemsAsync();
                var today     = DateOnly.FromDateTime(DateTime.Today);
                ExpiredItems  = allItems.Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value < today)
                                        .OrderBy(i => i.ExpiryDate).ToList();
                ExpiringItems = allItems.Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value >= today
                                                 && i.ExpiryDate.Value.DayNumber - today.DayNumber <= 90)
                                        .OrderBy(i => i.ExpiryDate).ToList();
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
