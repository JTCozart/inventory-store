using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Enums;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class ReportService : IReportService
{
    private readonly IInventoryRepository  _inventoryRepo;
    private readonly IActivityLogRepository _activityRepo;
    private readonly ICheckoutRepository   _checkoutRepo;
    private readonly IClientRepository     _clientRepo;

    public ReportService(
        IInventoryRepository inventoryRepo,
        IActivityLogRepository activityRepo,
        ICheckoutRepository checkoutRepo,
        IClientRepository clientRepo)
    {
        _inventoryRepo = inventoryRepo;
        _activityRepo  = activityRepo;
        _checkoutRepo  = checkoutRepo;
        _clientRepo    = clientRepo;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
    {
        var allItems        = (await _inventoryRepo.GetAllAsync()).ToList();
        var lowStock        = allItems.Where(i => i.IsLowStock).Select(InventoryService.MapToDto).ToList();
        var recentActivity  = (await _activityRepo.GetRecentAsync(10)).Select(MapLog).ToList();
        var activeRecords   = (await _checkoutRepo.GetAllActiveAsync()).ToList();
        var lostRecords     = (await _checkoutRepo.GetAllLostAsync()).ToList();

        var itemLookup   = allItems.ToDictionary(i => i.Id);
        var clientLookup = (await _clientRepo.GetAllAsync()).ToDictionary(c => c.Id, c => c.DisplayName);

        var checkedOutItems = activeRecords
            .OrderByDescending(r => (DateTime.UtcNow - r.CheckedOutAt).TotalDays)
            .Select(r =>
            {
                itemLookup.TryGetValue(r.InventoryItemId, out var item);
                var clientName = r.ClientId.HasValue ? clientLookup.GetValueOrDefault(r.ClientId.Value) : null;
                return new CheckedOutItemRow(
                    r.InventoryItemId,
                    item?.Name ?? "Unknown",
                    item?.Location,
                    item?.SKU,
                    r.CheckedOutBy,
                    clientName,
                    r.Quantity,
                    r.CheckedOutAt,
                    (int)(DateTime.UtcNow - r.CheckedOutAt).TotalDays);
            })
            .ToList();

        var lostItems = lostRecords
            .OrderByDescending(r => r.CheckedInAt ?? r.CheckedOutAt)
            .Select(r =>
            {
                itemLookup.TryGetValue(r.InventoryItemId, out var item);
                var clientName = r.ClientId.HasValue ? clientLookup.GetValueOrDefault(r.ClientId.Value) : null;
                return new LostItemRow(
                    r.InventoryItemId,
                    item?.Name ?? "Unknown",
                    item?.Location,
                    item?.SKU,
                    r.CheckedOutBy,
                    clientName,
                    r.Quantity,
                    r.CheckedInAt ?? r.CheckedOutAt);
            })
            .ToList();

        return new DashboardSummaryDto(
            allItems.Count,
            allItems.Sum(i => i.Quantity),
            lowStock.Count,
            activeRecords.Sum(c => c.Quantity),
            lostRecords.Sum(c => c.Quantity),
            lowStock,
            checkedOutItems,
            lostItems,
            recentActivity
        );
    }

    public async Task<StockReportDto> GetStockReportAsync()
    {
        var allItems = (await _inventoryRepo.GetAllAsync()).Select(InventoryService.MapToDto).ToList();
        var lowStock = allItems.Where(i => i.IsLowStock).ToList();
        return new StockReportDto(
            allItems, lowStock, allItems.Count,
            allItems.Sum(i => i.Quantity), lowStock.Count,
            DateTime.UtcNow);
    }

    public async Task<CheckedOutReportDto> GetCheckedOutReportAsync()
    {
        var records      = (await _checkoutRepo.GetAllActiveAsync()).ToList();
        var itemLookup   = (await _inventoryRepo.GetAllAsync()).ToDictionary(i => i.Id);
        var clientLookup = (await _clientRepo.GetAllAsync()).ToDictionary(c => c.Id, c => c.DisplayName);

        var rows = records.Select(r =>
        {
            itemLookup.TryGetValue(r.InventoryItemId, out var item);
            var clientName = r.ClientId.HasValue ? clientLookup.GetValueOrDefault(r.ClientId.Value) : null;
            return new CheckedOutItemRow(
                r.InventoryItemId,
                item?.Name ?? "Unknown",
                item?.Location,
                item?.SKU,
                r.CheckedOutBy,
                clientName,
                r.Quantity,
                r.CheckedOutAt,
                (int)(DateTime.UtcNow - r.CheckedOutAt).TotalDays);
        })
        .OrderByDescending(r => r.DaysOut)
        .ToList();

        return new CheckedOutReportDto(rows, rows.Sum(r => r.Quantity), DateTime.UtcNow);
    }

    public async Task<LostItemsReportDto> GetLostItemsReportAsync()
    {
        var records      = (await _checkoutRepo.GetAllLostAsync()).ToList();
        var itemLookup   = (await _inventoryRepo.GetAllAsync()).ToDictionary(i => i.Id);
        var clientLookup = (await _clientRepo.GetAllAsync()).ToDictionary(c => c.Id, c => c.DisplayName);

        var rows = records.Select(r =>
        {
            itemLookup.TryGetValue(r.InventoryItemId, out var item);
            var clientName = r.ClientId.HasValue ? clientLookup.GetValueOrDefault(r.ClientId.Value) : null;
            return new LostItemRow(
                r.InventoryItemId,
                item?.Name ?? "Unknown",
                item?.Location,
                item?.SKU,
                r.CheckedOutBy,
                clientName,
                r.Quantity,
                r.CheckedInAt ?? r.CheckedOutAt);
        })
        .OrderByDescending(r => r.LostAt)
        .ToList();

        return new LostItemsReportDto(rows, rows.Sum(r => r.Quantity), DateTime.UtcNow);
    }

    public async Task<TakeInventoryReportDto> GetTakeInventoryReportAsync()
    {
        var rows = (await _inventoryRepo.GetAllAsync())
            .OrderBy(i => i.Location)
            .ThenBy(i => i.Name)
            .Select(i =>
            {
                var (itemType, checkedOut, lost) = i switch
                {
                    ReusableItem r  => (ItemType.Reusable,  r.CheckedOutCount, r.LostCount),
                    ConsumableItem  => (ItemType.Consumable, 0,                 0),
                    _               => throw new InvalidOperationException($"Unknown type: {i.GetType().Name}")
                };
                return new TakeInventoryRow(
                    i.Id, i.Name, i.SKU, i.Location, itemType.ToString(),
                    i.Quantity, i.AvailableQuantity, checkedOut, lost);
            });

        return new TakeInventoryReportDto(rows, DateTime.UtcNow);
    }

    public async Task<IEnumerable<ActivityLogDto>> GetActivityReportAsync(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        IEnumerable<ActivityLog> logs;
        if (fromUtc.HasValue || toUtc.HasValue)
        {
            var start = fromUtc ?? DateTime.MinValue;
            var end   = toUtc   ?? DateTime.UtcNow;
            logs = await _activityRepo.GetByDateRangeAsync(start, end);
        }
        else
        {
            logs = await _activityRepo.GetAllAsync(pageSize: 500);
        }
        return logs.Select(MapLog);
    }

    private static ActivityLogDto MapLog(ActivityLog l) => new(
        l.Id, l.Username, l.Action, l.EntityType, l.EntityId, l.Details, l.Timestamp
    );
}
