using InventoryTracker.Domain.Enums;

namespace InventoryTracker.Application.DTOs;

public record InventoryItemDto(
    int Id,
    string Name,
    int Quantity,
    int AvailableQuantity,
    string? Description,
    string? Location,
    string? SKU,
    int MinimumQuantity,
    ItemType ItemType,
    int CheckedOutCount,
    int LostCount,
    bool IsLowStock,
    string? ScanWarning,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateInventoryItemDto(
    string Name,
    int Quantity,
    string? Description,
    string? Location,
    string? SKU,
    int MinimumQuantity,
    ItemType ItemType,
    string? ScanWarning
);

// ItemType is intentionally excluded — an item's type cannot change after creation.
public record UpdateInventoryItemDto(
    string Name,
    int Quantity,
    string? Description,
    string? Location,
    string? SKU,
    int MinimumQuantity,
    string? ScanWarning
);
