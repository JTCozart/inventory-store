using InventoryStore.Domain.Enums;

namespace InventoryStore.Application.DTOs;

// One member line of a kit, with the member's current standalone availability.
public record KitComponentDto(
    int ComponentItemId,
    string Name,
    string? SKU,
    ItemType ItemType,
    int PerKitQuantity,
    int AvailableQuantity
);

// An active kit checkout (a kit handed out as a unit).
public record KitCheckoutDto(
    int Id,
    int KitItemId,
    string CheckedOutBy,
    int Quantity,
    DateTime CheckedOutAt,
    DateTime? CheckedInAt,
    bool IsLost,
    string? Notes,
    int? ClientId
);

// A member that doesn't have enough stock for the requested kit action.
public record KitShortageDto(
    int ComponentItemId,
    string Name,
    ItemType ItemType,
    int Required,
    int Available
);

public record LinkKitComponentDto(int KitItemId, int ComponentItemId, int Quantity);

// Request to check out or consume a kit. AllowPartialFallback = the user has already accepted
// proceeding with whatever is available (only honoured when the kit allows partials).
public record KitActionDto(
    int KitItemId,
    int Quantity,
    string? CheckedOutBy = null,
    int? ClientId = null,
    string? Notes = null,
    bool AllowPartialFallback = false
);

// Result of a kit checkout/consume attempt. When NeedsConfirmation is true the action did NOT run;
// Shortages lists what is short so the UI can offer cancel / proceed-with-available.
public record KitActionResultDto(
    bool Completed,
    bool NeedsConfirmation,
    bool AllowPartial,
    IReadOnlyList<KitShortageDto> Shortages
);

// ── Consumable reconciliation ──────────────────────────────────────────
// How many units of a consumable member were actually used for a kit checkout. The remainder
// (allocated - used) is returned to stock. The server always receives a "used" count; the UI
// converts a "remaining" count to used before posting.
public record KitConsumableUsageDto(int ConsumableItemId, int UsedQuantity);

// One consumable line awaiting reconciliation, with the amount allocated at checkout.
public record KitReconcileLineDto(int ConsumableItemId, string Name, int AllocatedQuantity);

// A kit checkout's consumables to reconcile, used by the check-in modal and the report.
public record KitReconcileDto(
    int KitCheckoutId,
    int KitItemId,
    string KitName,
    string CheckedOutBy,
    DateTime CheckedOutAt,
    DateTime? CheckedInAt,
    IReadOnlyList<KitReconcileLineDto> Lines
);
