using System.Collections.Concurrent;
using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Enums;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

// Whole-kit operations: editing a kit's contents and acting on the kit as a unit
// (checkout / consume / restock / check-in / lost). Reuses the same stock primitives the
// per-item CheckoutService uses, applied across every member in one go.
public class KitService : IKitService
{
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IKitRepository _kitRepo;
    private readonly ICheckoutRepository _checkoutRepo;
    private readonly IActivityLogRepository _activityRepo;
    private readonly IStockMovementRepository _stockMovementRepo;
    private readonly INtfyService _ntfy;
    private readonly IWebhookService _webhooks;

    // Per-item locks, mirroring CheckoutService, so kit and single-item stock math stay consistent.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _itemLocks = new();
    private static SemaphoreSlim GetItemLock(int itemId) =>
        _itemLocks.GetOrAdd(itemId, _ => new SemaphoreSlim(1, 1));

    public KitService(
        IInventoryRepository inventoryRepo,
        IKitRepository kitRepo,
        ICheckoutRepository checkoutRepo,
        IActivityLogRepository activityRepo,
        IStockMovementRepository stockMovementRepo,
        INtfyService ntfy,
        IWebhookService webhooks)
    {
        _inventoryRepo     = inventoryRepo;
        _kitRepo           = kitRepo;
        _checkoutRepo      = checkoutRepo;
        _activityRepo      = activityRepo;
        _stockMovementRepo = stockMovementRepo;
        _ntfy              = ntfy;
        _webhooks          = webhooks;
    }

    private async Task<KitItem> GetKitAsync(int id)
    {
        var item = await _inventoryRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Kit not found.");
        if (item is not KitItem kit)
            throw new InvalidOperationException("This item is not a kit.");
        return kit;
    }

    // ── Editing kit contents ──────────────────────────────────────────
    public async Task LinkComponentAsync(LinkKitComponentDto dto, int userId, string username)
    {
        var kit = await GetKitAsync(dto.KitItemId);

        if (dto.ComponentItemId == dto.KitItemId)
            throw new InvalidOperationException("A kit cannot contain itself.");

        var component = await _inventoryRepo.GetByIdAsync(dto.ComponentItemId)
            ?? throw new KeyNotFoundException("Component item not found.");
        if (component is KitItem)
            throw new InvalidOperationException("A kit cannot contain another kit.");

        var qty = Math.Max(1, dto.Quantity);

        var existing = await _kitRepo.GetComponentAsync(dto.KitItemId, dto.ComponentItemId);
        if (existing is null)
        {
            await _kitRepo.AddComponentAsync(new KitComponent
            {
                KitItemId = dto.KitItemId, ComponentItemId = dto.ComponentItemId, Quantity = qty
            });
        }
        else
        {
            existing.Quantity = qty;
            await _kitRepo.UpdateComponentAsync(existing);
        }

        await LogAsync(userId, username, "KitComponentLinked", kit.Id,
            $"Linked {qty}x '{component.Name}' to kit '{kit.Name}'");
    }

    public async Task UnlinkComponentAsync(int kitItemId, int componentItemId, int userId, string username)
    {
        var kit = await GetKitAsync(kitItemId);
        var component = await _inventoryRepo.GetByIdAsync(componentItemId);
        await _kitRepo.RemoveComponentAsync(kitItemId, componentItemId);
        await LogAsync(userId, username, "KitComponentUnlinked", kit.Id,
            $"Removed '{component?.Name ?? componentItemId.ToString()}' from kit '{kit.Name}'");
    }

    public async Task SetComponentQuantityAsync(int kitItemId, int componentItemId, int quantity, int userId, string username)
    {
        var kit = await GetKitAsync(kitItemId);
        var existing = await _kitRepo.GetComponentAsync(kitItemId, componentItemId)
            ?? throw new KeyNotFoundException("That item is not part of this kit.");
        existing.Quantity = Math.Max(1, quantity);
        await _kitRepo.UpdateComponentAsync(existing);
        await LogAsync(userId, username, "KitComponentQuantity", kit.Id,
            $"Set '{existing.ComponentItem?.Name ?? componentItemId.ToString()}' to {existing.Quantity} per kit in '{kit.Name}'");
    }

    // ── Whole-kit actions ─────────────────────────────────────────────
    public async Task<KitActionResultDto> CheckOutKitAsync(KitActionDto dto, int userId, string username)
    {
        var kit = await GetKitAsync(dto.KitItemId);
        if (kit.Components.Count == 0)
            throw new InvalidOperationException("This kit has no items. Add members before checking it out.");

        var qty = Math.Max(1, dto.Quantity);
        var members = kit.Components.ToList();

        var shortages = ComputeShortages(members, qty, consumablesOnly: false);
        if (shortages.Count > 0)
        {
            // Whole-only kit, or partials allowed but not yet confirmed → ask the UI.
            if (!kit.AllowPartial)
                return new KitActionResultDto(false, true, false, shortages);
            if (!dto.AllowPartialFallback)
                return new KitActionResultDto(false, true, true, shortages);
        }

        var ids = members.Select(m => m.ComponentItemId).Distinct().OrderBy(i => i).ToList();
        await LockAllAsync(ids);
        try
        {
            var kitCheckout = await _kitRepo.CreateCheckoutAsync(new KitCheckout
            {
                KitItemId    = kit.Id,
                CheckedOutBy = dto.CheckedOutBy ?? "",
                Quantity     = qty,
                ClientId     = dto.ClientId,
                Notes        = dto.Notes,
                CheckedOutAt = DateTime.UtcNow
            });

            foreach (var m in members)
            {
                var component = await _inventoryRepo.GetByIdAsync(m.ComponentItemId);
                if (component is null) continue;
                var need   = m.Quantity * qty;
                var actual = Math.Min(need, Math.Max(0, component.AvailableQuantity));
                if (actual <= 0) continue;

                if (component is ReusableItem reusable)
                {
                    await _checkoutRepo.CreateAsync(new CheckoutRecord
                    {
                        InventoryItemId = component.Id,
                        CheckedOutBy    = dto.CheckedOutBy ?? "",
                        Quantity        = actual,
                        ClientId        = dto.ClientId,
                        Notes           = dto.Notes,
                        CheckedOutAt    = DateTime.UtcNow,
                        KitCheckoutId   = kitCheckout.Id
                    });
                    reusable.CheckedOutCount += actual;
                    reusable.UpdatedAt        = DateTime.UtcNow;
                    await _inventoryRepo.UpdateAsync(reusable);
                }
                else if (component is ConsumableItem consumable)
                {
                    // A kit being checked out consumes its consumable members permanently.
                    consumable.Quantity  = Math.Max(0, consumable.Quantity - actual);
                    consumable.UpdatedAt = DateTime.UtcNow;
                    await _inventoryRepo.UpdateAsync(consumable);
                    await RecordConsumeAsync(consumable, actual, userId, username, $"Kit '{kit.Name}' checkout");
                }
            }

            await LogAsync(userId, username, "KitCheckOut", kit.Id,
                $"'{dto.CheckedOutBy}' checked out {qty}x kit '{kit.Name}'");
            _ = _ntfy.NotifyCheckoutAsync(kit.Name, dto.CheckedOutBy ?? "", null);
            _ = _webhooks.DispatchAsync("kit.checkout",
                new { kit = new { id = kit.Id, name = kit.Name }, actor = username, checkedOutBy = dto.CheckedOutBy, quantity = qty });
        }
        finally
        {
            ReleaseAll(ids);
        }

        return new KitActionResultDto(true, false, kit.AllowPartial, Array.Empty<KitShortageDto>());
    }

    public async Task<KitActionResultDto> ConsumeKitAsync(KitActionDto dto, int userId, string username)
    {
        var kit = await GetKitAsync(dto.KitItemId);
        var consumables = kit.Components.Where(c => c.ComponentItem is ConsumableItem).ToList();
        if (consumables.Count == 0)
            throw new InvalidOperationException("This kit has no consumable items to use.");

        var qty = Math.Max(1, dto.Quantity);

        var shortages = ComputeShortages(consumables, qty, consumablesOnly: true);
        if (shortages.Count > 0)
        {
            if (!kit.AllowPartial)
                return new KitActionResultDto(false, true, false, shortages);
            if (!dto.AllowPartialFallback)
                return new KitActionResultDto(false, true, true, shortages);
        }

        var ids = consumables.Select(m => m.ComponentItemId).Distinct().OrderBy(i => i).ToList();
        await LockAllAsync(ids);
        try
        {
            foreach (var m in consumables)
            {
                var component = await _inventoryRepo.GetByIdAsync(m.ComponentItemId);
                if (component is not ConsumableItem consumable) continue;
                var need   = m.Quantity * qty;
                var actual = Math.Min(need, Math.Max(0, consumable.Quantity));
                if (actual <= 0) continue;

                consumable.Quantity  = Math.Max(0, consumable.Quantity - actual);
                consumable.UpdatedAt = DateTime.UtcNow;
                await _inventoryRepo.UpdateAsync(consumable);
                await RecordConsumeAsync(consumable, actual, userId, username, $"Kit '{kit.Name}' consume");
            }

            await LogAsync(userId, username, "KitConsume", kit.Id,
                $"Consumed {qty}x kit '{kit.Name}'");
            _ = _webhooks.DispatchAsync("kit.consumed",
                new { kit = new { id = kit.Id, name = kit.Name }, actor = username, quantity = qty });
        }
        finally
        {
            ReleaseAll(ids);
        }

        return new KitActionResultDto(true, false, kit.AllowPartial, Array.Empty<KitShortageDto>());
    }

    public async Task RestockKitAsync(int kitItemId, int quantity, int userId, string username)
    {
        var kit = await GetKitAsync(kitItemId);
        var consumables = kit.Components.Where(c => c.ComponentItem is ConsumableItem).ToList();
        if (consumables.Count == 0)
            throw new InvalidOperationException("This kit has no consumable items to restock.");

        var qty = Math.Max(1, quantity);
        var ids = consumables.Select(m => m.ComponentItemId).Distinct().OrderBy(i => i).ToList();
        await LockAllAsync(ids);
        try
        {
            foreach (var m in consumables)
            {
                var component = await _inventoryRepo.GetByIdAsync(m.ComponentItemId);
                if (component is not ConsumableItem consumable) continue;
                var amount = m.Quantity * qty;
                consumable.Quantity += amount;
                consumable.UpdatedAt = DateTime.UtcNow;
                await _inventoryRepo.UpdateAsync(consumable);

                await _stockMovementRepo.AddAsync(new StockMovement
                {
                    InventoryItemId = consumable.Id, ChangeType = "Restock", Quantity = amount,
                    UserId = userId, Username = username, Notes = $"Kit '{kit.Name}' restock", Timestamp = DateTime.UtcNow
                });
                _ = _webhooks.DispatchAsync("item.restocked",
                    new { item = new { id = consumable.Id, name = consumable.Name }, actor = username, quantity = amount });
            }

            await LogAsync(userId, username, "KitRestock", kit.Id,
                $"Restocked {qty}x kit '{kit.Name}' worth of consumables");
        }
        finally
        {
            ReleaseAll(ids);
        }
    }

    public async Task CheckInKitAsync(int kitCheckoutId, int userId, string username)
    {
        var kitCheckout = await _kitRepo.GetCheckoutAsync(kitCheckoutId)
            ?? throw new KeyNotFoundException("Kit checkout not found.");
        if (kitCheckout.IsCheckedIn)
            throw new InvalidOperationException("This kit is already checked in.");
        if (kitCheckout.IsLost)
            throw new InvalidOperationException("This kit was marked lost and cannot be checked in.");

        foreach (var record in kitCheckout.ComponentCheckouts)
        {
            if (record.CheckedInAt.HasValue || record.IsLost) continue;
            await ReturnReusableAsync(record, lost: false);
        }

        kitCheckout.CheckedInAt = DateTime.UtcNow;
        await _kitRepo.UpdateCheckoutAsync(kitCheckout);

        var kit = await _inventoryRepo.GetByIdAsync(kitCheckout.KitItemId);
        await LogAsync(userId, username, "KitCheckIn", kitCheckout.KitItemId,
            $"'{kitCheckout.CheckedOutBy}' checked in kit '{kit?.Name ?? kitCheckout.KitItemId.ToString()}'");
        _ = _ntfy.NotifyCheckinAsync(kit?.Name ?? "Kit", kitCheckout.CheckedOutBy);
    }

    public async Task MarkKitLostAsync(int kitCheckoutId, int userId, string username)
    {
        var kitCheckout = await _kitRepo.GetCheckoutAsync(kitCheckoutId)
            ?? throw new KeyNotFoundException("Kit checkout not found.");
        if (kitCheckout.IsCheckedIn)
            throw new InvalidOperationException("Cannot mark a returned kit as lost.");
        if (kitCheckout.IsLost)
            throw new InvalidOperationException("This kit is already marked lost.");

        foreach (var record in kitCheckout.ComponentCheckouts)
        {
            if (record.CheckedInAt.HasValue || record.IsLost) continue;
            await ReturnReusableAsync(record, lost: true);
        }

        kitCheckout.IsLost      = true;
        kitCheckout.CheckedInAt = DateTime.UtcNow;
        await _kitRepo.UpdateCheckoutAsync(kitCheckout);

        var kit = await _inventoryRepo.GetByIdAsync(kitCheckout.KitItemId);
        await LogAsync(userId, username, "KitLost", kitCheckout.KitItemId,
            $"Kit '{kit?.Name ?? kitCheckout.KitItemId.ToString()}' (checked out by '{kitCheckout.CheckedOutBy}') marked as lost");
        _ = _ntfy.NotifyLostAsync(kit?.Name ?? "Kit", kitCheckout.CheckedOutBy);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    // Members short of the requested quantity. When consumablesOnly is false, reusables are
    // measured by available-to-check-out; consumables by stock on hand.
    private static List<KitShortageDto> ComputeShortages(IEnumerable<KitComponent> members, int qty, bool consumablesOnly)
    {
        var list = new List<KitShortageDto>();
        foreach (var m in members)
        {
            if (m.ComponentItem is null) continue;
            if (consumablesOnly && m.ComponentItem is not ConsumableItem) continue;
            var required  = m.Quantity * qty;
            var available = m.ComponentItem.AvailableQuantity;
            if (available < required)
            {
                var type = m.ComponentItem is ReusableItem ? ItemType.Reusable : ItemType.Consumable;
                list.Add(new KitShortageDto(m.ComponentItemId, m.ComponentItem.Name, type, required, available));
            }
        }
        return list;
    }

    // Returns a kit-member checkout record, either back to stock (lost: false) or to the lost pile.
    private async Task ReturnReusableAsync(CheckoutRecord record, bool lost)
    {
        record.CheckedInAt = DateTime.UtcNow;
        record.IsLost      = lost;
        await _checkoutRepo.UpdateAsync(record);

        if (await _inventoryRepo.GetByIdAsync(record.InventoryItemId) is ReusableItem reusable)
        {
            reusable.CheckedOutCount = Math.Max(0, reusable.CheckedOutCount - record.Quantity);
            if (lost) reusable.LostCount += record.Quantity;
            reusable.UpdatedAt = DateTime.UtcNow;
            await _inventoryRepo.UpdateAsync(reusable);
        }
    }

    private async Task RecordConsumeAsync(ConsumableItem consumable, int amount, int userId, string username, string note)
    {
        await _stockMovementRepo.AddAsync(new StockMovement
        {
            InventoryItemId = consumable.Id, ChangeType = "Consume", Quantity = amount,
            UserId = userId, Username = username, Notes = note, Timestamp = DateTime.UtcNow
        });
        _ = _webhooks.DispatchAsync("item.consumed",
            new { item = new { id = consumable.Id, name = consumable.Name }, actor = username, quantity = amount, notes = note });
        if (consumable.IsLowStock)
            _ = _ntfy.NotifyLowStockAsync(consumable.Name, consumable.Quantity, consumable.MinimumQuantity);
    }

    private Task LogAsync(int userId, string username, string action, int kitId, string details) =>
        _activityRepo.CreateAsync(new ActivityLog
        {
            UserId = userId, Username = username,
            Action = action, EntityType = "InventoryItem", EntityId = kitId, Details = details
        });

    private static async Task LockAllAsync(IEnumerable<int> ids)
    {
        foreach (var id in ids) await GetItemLock(id).WaitAsync();
    }

    private static void ReleaseAll(IEnumerable<int> ids)
    {
        foreach (var id in ids) GetItemLock(id).Release();
    }
}
