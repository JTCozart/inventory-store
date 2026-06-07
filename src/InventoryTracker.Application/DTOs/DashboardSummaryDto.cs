namespace InventoryTracker.Application.DTOs;

public record DashboardSummaryDto(
    int TotalItems,
    int TotalQuantity,
    int LowStockCount,
    int CheckedOutCount,
    int LostItemCount,
    IEnumerable<InventoryItemDto> LowStockItems,
    IEnumerable<CheckedOutItemRow> CheckedOutItems,
    IEnumerable<LostItemRow> LostItems,
    IEnumerable<ActivityLogDto> RecentActivity
);

public record ActivityLogDto(
    int Id,
    string Username,
    string Action,
    string? EntityType,
    int? EntityId,
    string? Details,
    DateTime Timestamp
);

public record StockReportDto(
    IEnumerable<InventoryItemDto> AllItems,
    IEnumerable<InventoryItemDto> LowStockItems,
    int TotalItems,
    int TotalQuantity,
    int LowStockCount,
    DateTime GeneratedAt
);

public record CheckedOutReportDto(
    IEnumerable<CheckedOutItemRow> Items,
    int TotalCheckedOut,
    DateTime GeneratedAt
);

public record CheckedOutItemRow(
    int InventoryItemId,
    string ItemName,
    string? ItemLocation,
    string? SKU,
    string CheckedOutBy,
    int Quantity,
    DateTime CheckedOutAt,
    int DaysOut
);

public record LostItemsReportDto(
    IEnumerable<LostItemRow> Items,
    int TotalLost,
    DateTime GeneratedAt
);

public record LostItemRow(
    int InventoryItemId,
    string ItemName,
    string? ItemLocation,
    string? SKU,
    string LastCheckedOutBy,
    int Quantity,
    DateTime LostAt
);

public record TakeInventoryReportDto(
    IEnumerable<TakeInventoryRow> Items,
    DateTime GeneratedAt
);

public record TakeInventoryRow(
    int Id,
    string Name,
    string? SKU,
    string? Location,
    string ItemType,
    int ExpectedQuantity,
    int AvailableQuantity,
    int CheckedOutCount,
    int LostCount
);
