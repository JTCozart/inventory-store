using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IKitRepository
{
    // ── Components (kit contents) ──────────────────────────────────────
    Task<KitComponent?> GetComponentAsync(int kitItemId, int componentItemId);
    Task AddComponentAsync(KitComponent component);
    Task UpdateComponentAsync(KitComponent component);
    Task RemoveComponentAsync(int kitItemId, int componentItemId);
    // Removes every kit-member line that points at the given item (used when that item is deleted).
    Task RemoveComponentsReferencingItemAsync(int componentItemId);

    // ── Kit checkouts (a kit handed out as a unit) ─────────────────────
    Task<KitCheckout> CreateCheckoutAsync(KitCheckout checkout);
    Task<KitCheckout?> GetCheckoutAsync(int id);
    Task UpdateCheckoutAsync(KitCheckout checkout);
    Task<IEnumerable<KitCheckout>> GetActiveByKitAsync(int kitItemId);
    Task<IEnumerable<KitCheckout>> GetAllActiveAsync();
    // Deletes a kit's own checkout groups (its member CheckoutRecords are cleared separately).
    Task DeleteByKitItemIdAsync(int kitItemId);

    // ── Consumable reconciliation ──────────────────────────────────────
    Task AddAllocationAsync(KitConsumableAllocation allocation);
    Task UpdateAllocationAsync(KitConsumableAllocation allocation);
    // Checked-in kits still awaiting a consumable count (NeedsReconciliation), newest first.
    Task<IEnumerable<KitCheckout>> GetPendingReconciliationsAsync();
}
