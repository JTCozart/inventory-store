namespace InventoryStore.Application.DTOs;

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
    string? ClientName,
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
    string? ClientName,
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

// Kits that can't currently be assembled as a whole (buildable count is zero), with the
// members responsible for the shortfall.
public record IncompleteKitsReportDto(
    IEnumerable<IncompleteKitRow> Items,
    int TotalIncomplete,
    DateTime GeneratedAt
);

public record IncompleteKitRow(
    int KitId,
    string KitName,
    string? SKU,
    string? Location,
    int Buildable,
    int MemberCount,
    IReadOnlyList<KitShortfallRow> Shortfalls
);

public record KitShortfallRow(
    string ItemName,
    string ItemType,
    int PerKit,
    int Available,
    int Shortfall
);
