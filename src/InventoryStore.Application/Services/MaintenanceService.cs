using System.Collections.Concurrent;
using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

// Maintenance schedules and "out for maintenance" visits. Sending a reusable item out reduces its
// available quantity (mirroring CheckoutService); returning it restores availability and stamps the
// schedule's last-maintained date. Uses the same per-item lock as checkouts so stock math stays consistent.
public class MaintenanceService : IMaintenanceService
{
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IMaintenanceRepository _maintenanceRepo;
    private readonly IVendorRepository _vendorRepo;
    private readonly IActivityLogRepository _activityRepo;
    private readonly IWebhookService _webhooks;

    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _itemLocks = new();
    private static SemaphoreSlim GetItemLock(int itemId) =>
        _itemLocks.GetOrAdd(itemId, _ => new SemaphoreSlim(1, 1));

    public MaintenanceService(
        IInventoryRepository inventoryRepo,
        IMaintenanceRepository maintenanceRepo,
        IVendorRepository vendorRepo,
        IActivityLogRepository activityRepo,
        IWebhookService webhooks)
    {
        _inventoryRepo   = inventoryRepo;
        _maintenanceRepo = maintenanceRepo;
        _vendorRepo      = vendorRepo;
        _activityRepo    = activityRepo;
        _webhooks        = webhooks;
    }

    public async Task<MaintenanceItemStatusDto> GetItemStatusAsync(int inventoryItemId)
    {
        var schedule = await _maintenanceRepo.GetScheduleAsync(inventoryItemId);
        var openVisit = await _maintenanceRepo.GetOpenVisitAsync(inventoryItemId);
        Vendor? vendor = openVisit?.VendorId is { } vid ? await _vendorRepo.GetByIdAsync(vid) : null;

        return new MaintenanceItemStatusDto(
            inventoryItemId,
            schedule is null ? null : MapSchedule(schedule),
            openVisit is null ? null : MapVisit(openVisit, vendor?.Name));
    }

    public async Task SaveScheduleAsync(SaveMaintenanceScheduleDto dto, int userId, string username)
    {
        var item = await _inventoryRepo.GetByIdAsync(dto.InventoryItemId)
            ?? throw new KeyNotFoundException("Item not found.");

        var existing = await _maintenanceRepo.GetScheduleAsync(dto.InventoryItemId);
        var schedule = existing ?? new MaintenanceSchedule { InventoryItemId = dto.InventoryItemId };
        schedule.LastMaintainedDate = dto.LastMaintainedDate;
        schedule.IntervalValue      = Math.Max(0, dto.IntervalValue);
        schedule.IntervalUnit       = dto.IntervalUnit;
        schedule.Notes              = dto.Notes?.Trim();
        schedule.UpdatedAt          = DateTime.UtcNow;
        await _maintenanceRepo.UpsertScheduleAsync(schedule);

        await LogAsync(userId, username, "MaintenanceSchedule", item.Id,
            $"Updated maintenance schedule for '{item.Name}'");
    }

    public async Task MarkOutAsync(MarkOutForMaintenanceDto dto, int userId, string username)
    {
        var itemLock = GetItemLock(dto.InventoryItemId);
        await itemLock.WaitAsync();
        try
        {
            var item = await _inventoryRepo.GetByIdAsync(dto.InventoryItemId)
                ?? throw new KeyNotFoundException("Item not found.");

            if (item is not ReusableItem reusable)
                throw new InvalidOperationException("Only reusable items can be sent out for maintenance.");

            var existingOpen = await _maintenanceRepo.GetOpenVisitAsync(dto.InventoryItemId);
            if (existingOpen is not null)
                throw new InvalidOperationException("This item is already out for maintenance.");

            var qty = Math.Max(1, dto.Quantity);
            if (reusable.AvailableQuantity < qty)
                throw new InvalidOperationException(
                    $"Only {reusable.AvailableQuantity} available. Cannot send out {qty} for maintenance.");

            string? vendorName = null;
            if (dto.VendorId is { } vid)
                vendorName = (await _vendorRepo.GetByIdAsync(vid))?.Name;

            await _maintenanceRepo.CreateVisitAsync(new MaintenanceVisit
            {
                InventoryItemId     = dto.InventoryItemId,
                Quantity            = qty,
                VendorId            = dto.VendorId,
                Notes               = dto.Notes?.Trim(),
                OutForMaintenanceAt = DateTime.UtcNow
            });

            reusable.OutForMaintenanceCount += qty;
            reusable.UpdatedAt = DateTime.UtcNow;
            await _inventoryRepo.UpdateAsync(reusable);

            await LogAsync(userId, username, "MaintenanceOut", item.Id,
                $"Sent {qty}x '{item.Name}' out for maintenance"
                + (vendorName is not null ? $" to '{vendorName}'" : ""));
            _ = _webhooks.DispatchAsync("maintenance.out",
                new { item = new { id = item.Id, name = item.Name }, actor = username, quantity = qty, vendor = vendorName });
        }
        finally
        {
            itemLock.Release();
        }
    }

    public async Task ReturnAsync(int inventoryItemId, string? notes, int userId, string username)
    {
        var itemLock = GetItemLock(inventoryItemId);
        await itemLock.WaitAsync();
        try
        {
            var item = await _inventoryRepo.GetByIdAsync(inventoryItemId)
                ?? throw new KeyNotFoundException("Item not found.");

            var visit = await _maintenanceRepo.GetOpenVisitAsync(inventoryItemId)
                ?? throw new InvalidOperationException("This item is not currently out for maintenance.");

            visit.ReturnedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(notes)) visit.Notes = notes.Trim();
            await _maintenanceRepo.UpdateVisitAsync(visit);

            if (item is ReusableItem reusable)
            {
                reusable.OutForMaintenanceCount = Math.Max(0, reusable.OutForMaintenanceCount - visit.Quantity);
                reusable.UpdatedAt = DateTime.UtcNow;
                await _inventoryRepo.UpdateAsync(reusable);
            }

            // Returning from service updates the last-maintained date so the next-due date rolls forward.
            var schedule = await _maintenanceRepo.GetScheduleAsync(inventoryItemId)
                ?? new MaintenanceSchedule { InventoryItemId = inventoryItemId };
            schedule.LastMaintainedDate = DateOnly.FromDateTime(DateTime.Today);
            schedule.UpdatedAt = DateTime.UtcNow;
            await _maintenanceRepo.UpsertScheduleAsync(schedule);

            await LogAsync(userId, username, "MaintenanceReturn", item.Id,
                $"Returned {visit.Quantity}x '{item.Name}' from maintenance");
            _ = _webhooks.DispatchAsync("maintenance.returned",
                new { item = new { id = item.Id, name = item.Name }, actor = username, quantity = visit.Quantity });
        }
        finally
        {
            itemLock.Release();
        }
    }

    private Task LogAsync(int userId, string username, string action, int itemId, string details) =>
        _activityRepo.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = action, EntityType = "InventoryItem", EntityId = itemId, Details = details
        });

    private static MaintenanceScheduleDto MapSchedule(MaintenanceSchedule s) => new(
        s.InventoryItemId, s.LastMaintainedDate, s.IntervalValue, s.IntervalUnit,
        s.Notes, s.NextDueDate, s.IsOverdue);

    private static MaintenanceVisitDto MapVisit(MaintenanceVisit v, string? vendorName) => new(
        v.Id, v.InventoryItemId, v.Quantity, v.VendorId, vendorName,
        v.OutForMaintenanceAt, v.ReturnedAt, v.Notes, v.IsOut);
}
