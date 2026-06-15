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
