using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface IKitService
{
    // Edit-mode wiring: link / unlink / re-quantify a kit's members.
    Task LinkComponentAsync(LinkKitComponentDto dto, int userId, string username);
    Task UnlinkComponentAsync(int kitItemId, int componentItemId, int userId, string username);
    Task SetComponentQuantityAsync(int kitItemId, int componentItemId, int quantity, int userId, string username);

    // Whole-kit checkout. Returns a result that may ask the UI to confirm a partial run when some
    // members are short.
    Task<KitActionResultDto> CheckOutKitAsync(KitActionDto dto, int userId, string username);

    // Check a kit back in. When usage is supplied the consumables are reconciled inline (unused
    // remainder returned to stock); when it is null and flagIfNotReconciled is set, the checkout is
    // flagged so it shows up on the reconciliation report to be counted later.
    Task CheckInKitAsync(int kitCheckoutId, IReadOnlyList<KitConsumableUsageDto>? usage,
        bool flagIfNotReconciled, int userId, string username);
    Task MarkKitLostAsync(int kitCheckoutId, int userId, string username);

    // ── Consumable reconciliation ──────────────────────────────────────
    // Reconcile a kit that was checked in without recording usage (the report path).
    Task ReconcileKitAsync(int kitCheckoutId, IReadOnlyList<KitConsumableUsageDto> usage,
        int userId, string username);
    // Kits checked in but still awaiting a consumable count.
    Task<IReadOnlyList<KitReconcileDto>> GetPendingReconciliationsAsync();
    // One kit's consumable lines (allocated amounts) for the reconcile/check-in modal.
    Task<KitReconcileDto?> GetReconciliationAsync(int kitCheckoutId);
}
