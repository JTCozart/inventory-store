using InventoryTracker.Domain.Enums;

namespace InventoryTracker.Application.DTOs;

public record CheckoutRecordDto(
    int Id,
    int InventoryItemId,
    string ItemName,
    string CheckedOutBy,
    int Quantity,
    DateTime CheckedOutAt,
    DateTime? CheckedInAt,
    bool IsLost,
    string? Notes,
    int? ClientId
);

public record ItemStatusDto(
    int Id,
    string Name,
    string? SKU,
    string? Location,
    ItemType ItemType,
    int Quantity,
    int AvailableQuantity,
    int CheckedOutCount,
    int LostCount,
    bool IsLowStock,
    int MinimumQuantity,
    string? ScanWarning,
    IEnumerable<CheckoutRecordDto> ActiveCheckouts,
    IEnumerable<CheckoutRecordDto> LostCheckouts
);

public record CheckOutItemDto(
    int InventoryItemId,
    string CheckedOutBy,
    int Quantity,
    string? Notes,
    int? ClientId = null
);

public record CheckInItemDto(
    int CheckoutRecordId,
    string? Notes
);

public record MarkLostDto(
    int CheckoutRecordId,
    string? Notes
);

public record MarkFoundDto(
    int CheckoutRecordId,
    string? Notes
);

public record ConsumeItemDto(
    int InventoryItemId,
    int Quantity,
    string? Notes
);

public record RestockItemDto(
    int InventoryItemId,
    int Quantity,
    string? Notes
);
