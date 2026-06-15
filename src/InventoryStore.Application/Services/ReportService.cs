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
    private readonly IMaintenanceRepository _maintenanceRepo;
    private readonly IVendorRepository     _vendorRepo;
    private readonly IUserRepository       _userRepo;
    private readonly IHostingMode          _hostingMode;

    public ReportService(
        IInventoryRepository inventoryRepo,
        IActivityLogRepository activityRepo,
        ICheckoutRepository checkoutRepo,
        IClientRepository clientRepo,
        IMaintenanceRepository maintenanceRepo,
        IVendorRepository vendorRepo,
        IUserRepository userRepo,
        IHostingMode hostingMode)
    {
        _inventoryRepo   = inventoryRepo;
        _activityRepo    = activityRepo;
        _checkoutRepo    = checkoutRepo;
        _clientRepo      = clientRepo;
        _maintenanceRepo = maintenanceRepo;
        _vendorRepo      = vendorRepo;
        _userRepo        = userRepo;
        _hostingMode     = hostingMode;
    }

    // In professional-services hosted mode the locked first-admin account is shown as "SYSTEM" in
    // activity logs. Returns its id so log rows by that user can be relabelled; null otherwise.
    private async Task<int?> GetLockedAdminIdAsync() =>
        _hostingMode.IsProfessionalServicesHosted
            ? (await _userRepo.GetAdminAsync())?.Id
            : null;

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
    {
        var lockedAdminId   = await GetLockedAdminIdAsync();
        var allItems        = (await _inventoryRepo.GetAllAsync()).ToList();
        var lowStock        = allItems.Where(i => i.IsLowStock).Select(InventoryService.MapToDto).ToList();
        var recentActivity  = (await _activityRepo.GetRecentAsync(10)).Select(l => MapLog(l, lockedAdminId)).ToList();
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
                    KitItem         => (ItemType.Kit,        0,                 0),
                    ConsumableItem  => (ItemType.Consumable, 0,                 0),
                    _               => throw new InvalidOperationException($"Unknown type: {i.GetType().Name}")
                };
                return new TakeInventoryRow(
                    i.Id, i.Name, i.SKU, i.Location, itemType.ToString(),
                    i.Quantity, i.AvailableQuantity, checkedOut, lost);
            });

        return new TakeInventoryReportDto(rows, DateTime.UtcNow);
    }

    public async Task<IncompleteKitsReportDto> GetIncompleteKitsReportAsync()
    {
        // A kit is "incomplete" when it can't be assembled even once (buildable == 0). That happens
        // when a member is short for a single kit, or the kit has no members yet.
        var kits = (await _inventoryRepo.GetAllAsync())
            .OfType<KitItem>()
            .Select(InventoryService.MapToDto)
            .Where(k => k.BuildableQuantity == 0)
            .OrderBy(k => k.Name)
            .ToList();

        var rows = kits.Select(k =>
        {
            var components = k.Components ?? [];
            var shortfalls = components
                .Where(c => c.AvailableQuantity < c.PerKitQuantity)
                .OrderByDescending(c => c.PerKitQuantity - c.AvailableQuantity)
                .Select(c => new KitShortfallRow(
                    c.Name, c.ItemType.ToString(), c.PerKitQuantity, c.AvailableQuantity,
                    c.PerKitQuantity - c.AvailableQuantity))
                .ToList();
            return new IncompleteKitRow(k.Id, k.Name, k.SKU, k.Location, k.BuildableQuantity, components.Count, shortfalls);
        }).ToList();

        return new IncompleteKitsReportDto(rows, rows.Count, DateTime.UtcNow);
    }

    public async Task<MaintenanceReportDto> GetMaintenanceDueReportAsync()
    {
        var schedules  = await _maintenanceRepo.GetAllSchedulesAsync();
        var openVisits = await _maintenanceRepo.GetAllOpenVisitsAsync();
        var itemLookup = (await _inventoryRepo.GetAllAsync()).ToDictionary(i => i.Id);
        var vendorLookup = (await _vendorRepo.GetAllAsync()).ToDictionary(v => v.Id, v => v.Name);

        var openByItem = openVisits
            .GroupBy(v => v.InventoryItemId)
            .ToDictionary(g => g.Key, g => g.First());

        // Item ids that have a schedule and/or an open visit.
        var itemIds = schedules.Select(s => s.InventoryItemId)
            .Union(openVisits.Select(v => v.InventoryItemId))
            .Distinct();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var scheduleByItem = schedules.ToDictionary(s => s.InventoryItemId);

        var rows = new List<MaintenanceReportRow>();
        foreach (var id in itemIds)
        {
            if (!itemLookup.TryGetValue(id, out var item)) continue;
            scheduleByItem.TryGetValue(id, out var schedule);
            openByItem.TryGetValue(id, out var visit);

            var next       = schedule?.NextDueDate;
            var isOverdue  = next is { } d && d < today;
            var isDueSoon  = next is { } d2 && !isOverdue && d2 <= today.AddDays(14);
            var isOut      = visit is not null;
            var vendorName = visit?.VendorId is { } vid ? vendorLookup.GetValueOrDefault(vid) : null;

            rows.Add(new MaintenanceReportRow(
                item.Id, item.Name, item.SKU, item.Location,
                schedule?.LastMaintainedDate, next, isOverdue, isDueSoon, isOut, vendorName));
        }

        var ordered = rows
            .OrderByDescending(r => r.IsOverdue)
            .ThenByDescending(r => r.IsOut)
            .ThenBy(r => r.NextDueDate ?? DateOnly.MaxValue)
            .ToList();

        return new MaintenanceReportDto(
            ordered,
            ordered.Count(r => r.IsOverdue),
            ordered.Count(r => r.IsDueSoon),
            ordered.Count(r => r.IsOut),
            DateTime.UtcNow);
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
        var lockedAdminId = await GetLockedAdminIdAsync();
        return logs.Select(l => MapLog(l, lockedAdminId));
    }

    private static ActivityLogDto MapLog(ActivityLog l, int? lockedAdminId = null) => new(
        l.Id,
        lockedAdminId is not null && l.UserId == lockedAdminId ? "SYSTEM" : l.Username,
        l.Action, l.EntityType, l.EntityId, l.Details, l.Timestamp
    );
}
