using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface IReportService
{
    Task<StockReportDto>        GetStockReportAsync();
    Task<CheckedOutReportDto>   GetCheckedOutReportAsync();
    Task<LostItemsReportDto>    GetLostItemsReportAsync();
    Task<TakeInventoryReportDto> GetTakeInventoryReportAsync();
    Task<IEnumerable<ActivityLogDto>> GetActivityReportAsync(DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<DashboardSummaryDto>   GetDashboardSummaryAsync();
}
