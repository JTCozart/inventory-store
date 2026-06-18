using System.Collections.Concurrent;
using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Enums;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

// Whole-kit operations: editing a kit's contents and acting on the kit as a unit
// (checkout / check-in / reconcile / lost). Reuses the same stock primitives the
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
                    // A kit being checked out draws down its consumable members. We record how much
                    // was allocated so the unused remainder can be returned when the kit is checked
                    // back in and someone counts what was actually used.
                    consumable.Quantity  = Math.Max(0, consumable.Quantity - actual);
                    consumable.UpdatedAt = DateTime.UtcNow;
                    await _inventoryRepo.UpdateAsync(consumable);
                    await RecordConsumeAsync(consumable, actual, userId, username, $"Kit '{kit.Name}' checkout");
                    await _kitRepo.AddAllocationAsync(new KitConsumableAllocation
                    {
                        KitCheckoutId     = kitCheckout.Id,
                        ConsumableItemId  = consumable.Id,
                        AllocatedQuantity = actual,
                        UsedQuantity      = actual
                    });
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

    public async Task CheckInKitAsync(int kitCheckoutId, IReadOnlyList<KitConsumableUsageDto>? usage,
        bool flagIfNotReconciled, int userId, string username)
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

        var hasAllocations = kitCheckout.ConsumableAllocations.Any(a => a.ReconciledAt == null);
        if (usage is not null)
            await ApplyReconciliationAsync(kitCheckout, usage, userId, username);
        else if (flagIfNotReconciled && hasAllocations)
            kitCheckout.NeedsReconciliation = true;

        await _kitRepo.UpdateCheckoutAsync(kitCheckout);

        var kit = await _inventoryRepo.GetByIdAsync(kitCheckout.KitItemId);
        await LogAsync(userId, username, "KitCheckIn", kitCheckout.KitItemId,
            $"'{kitCheckout.CheckedOutBy}' checked in kit '{kit?.Name ?? kitCheckout.KitItemId.ToString()}'");
        _ = _ntfy.NotifyCheckinAsync(kit?.Name ?? "Kit", kitCheckout.CheckedOutBy);
    }

    // ── Consumable reconciliation ──────────────────────────────────────
    public async Task ReconcileKitAsync(int kitCheckoutId, IReadOnlyList<KitConsumableUsageDto> usage,
        int userId, string username)
    {
        var kitCheckout = await _kitRepo.GetCheckoutAsync(kitCheckoutId)
            ?? throw new KeyNotFoundException("Kit checkout not found.");
        await ApplyReconciliationAsync(kitCheckout, usage, userId, username);
        await _kitRepo.UpdateCheckoutAsync(kitCheckout);
    }

    public async Task<IReadOnlyList<KitReconcileDto>> GetPendingReconciliationsAsync()
    {
        var pending = await _kitRepo.GetPendingReconciliationsAsync();
        var result = new List<KitReconcileDto>();
        foreach (var k in pending)
        {
            var dto = await BuildReconcileDtoAsync(k);
            if (dto is not null) result.Add(dto);
        }
        return result;
    }

    public async Task<KitReconcileDto?> GetReconciliationAsync(int kitCheckoutId)
    {
        var kitCheckout = await _kitRepo.GetCheckoutAsync(kitCheckoutId);
        return kitCheckout is null ? null : await BuildReconcileDtoAsync(kitCheckout);
    }

    private async Task<KitReconcileDto?> BuildReconcileDtoAsync(KitCheckout kitCheckout)
    {
        var kit = await _inventoryRepo.GetByIdAsync(kitCheckout.KitItemId);
        var lines = new List<KitReconcileLineDto>();
        foreach (var a in kitCheckout.ConsumableAllocations.Where(a => a.ReconciledAt == null))
        {
            var item = await _inventoryRepo.GetByIdAsync(a.ConsumableItemId);
            lines.Add(new KitReconcileLineDto(a.ConsumableItemId,
                item?.Name ?? a.ConsumableItemId.ToString(), a.AllocatedQuantity));
        }
        return new KitReconcileDto(kitCheckout.Id, kitCheckout.KitItemId,
            kit?.Name ?? kitCheckout.KitItemId.ToString(), kitCheckout.CheckedOutBy,
            kitCheckout.CheckedOutAt, kitCheckout.CheckedInAt, lines);
    }

    // Returns the unused remainder of each consumable allocation to stock and clears the flag.
    // The caller persists the kit checkout afterwards.
    private async Task ApplyReconciliationAsync(KitCheckout kitCheckout,
        IReadOnlyList<KitConsumableUsageDto> usage, int userId, string username)
    {
        var allocs = kitCheckout.ConsumableAllocations.Where(a => a.ReconciledAt == null).ToList();
        if (allocs.Count > 0)
        {
            var usageMap = usage
                .GroupBy(u => u.ConsumableItemId)
                .ToDictionary(g => g.Key, g => g.Last().UsedQuantity);

            var ids = allocs.Select(a => a.ConsumableItemId).Distinct().OrderBy(i => i).ToList();
            await LockAllAsync(ids);
            try
            {
                foreach (var a in allocs)
                {
                    var used = usageMap.TryGetValue(a.ConsumableItemId, out var u)
                        ? Math.Clamp(u, 0, a.AllocatedQuantity)
                        : a.AllocatedQuantity; // no figure given for this line → treat as fully used
                    a.UsedQuantity = used;
                    a.ReconciledAt = DateTime.UtcNow;

                    var returnQty = a.AllocatedQuantity - used;
                    if (returnQty > 0 && await _inventoryRepo.GetByIdAsync(a.ConsumableItemId) is ConsumableItem consumable)
                    {
                        consumable.Quantity += returnQty;
                        consumable.UpdatedAt = DateTime.UtcNow;
                        await _inventoryRepo.UpdateAsync(consumable);

                        await _stockMovementRepo.AddAsync(new StockMovement
                        {
                            InventoryItemId = consumable.Id, ChangeType = "Restock", Quantity = returnQty,
                            UserId = userId, Username = username,
                            Notes = "Kit reconcile: unused consumable returned", Timestamp = DateTime.UtcNow
                        });
                        _ = _webhooks.DispatchAsync("item.restocked",
                            new { item = new { id = consumable.Id, name = consumable.Name }, actor = username, quantity = returnQty });
                    }

                    await _kitRepo.UpdateAllocationAsync(a);
                }
            }
            finally
            {
                ReleaseAll(ids);
            }
        }

        kitCheckout.NeedsReconciliation = false;
        kitCheckout.ReconciledAt = DateTime.UtcNow;

        var kit = await _inventoryRepo.GetByIdAsync(kitCheckout.KitItemId);
        await LogAsync(userId, username, "KitReconcile", kitCheckout.KitItemId,
            $"Reconciled consumables for kit '{kit?.Name ?? kitCheckout.KitItemId.ToString()}' (checked out by '{kitCheckout.CheckedOutBy}')");
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
