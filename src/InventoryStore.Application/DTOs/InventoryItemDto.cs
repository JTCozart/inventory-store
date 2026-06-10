using InventoryStore.Domain.Enums;

namespace InventoryStore.Application.DTOs;

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
    DateTime UpdatedAt,
    int? CategoryId,
    string? CategoryName,
    DateOnly? ExpiryDate,
    bool IsPublic,
    string? CategoryColor,
    string? MetadataImageUrl = null,
    string? MetadataBrand = null,
    string? MetadataCategory = null,
    string? MetadataDescription = null
);

public record CreateInventoryItemDto(
    string Name,
    int Quantity,
    string? Description,
    string? Location,
    string? SKU,
    int MinimumQuantity,
    ItemType ItemType,
    string? ScanWarning,
    int? CategoryId,
    DateOnly? ExpiryDate,
    int? SelectedMetadataId = null
);

public record LocationSummaryDto(string Name, int Count);

// ItemType is intentionally excluded — an item's type cannot change after creation.
public record UpdateInventoryItemDto(
    string Name,
    int Quantity,
    string? Description,
    string? Location,
    string? SKU,
    int MinimumQuantity,
    string? ScanWarning,
    int? CategoryId,
    DateOnly? ExpiryDate,
    int? SelectedMetadataId = null
);
