using InventoryStore.Domain.Enums;

namespace InventoryStore.Application.DTOs;

public record MaintenanceScheduleDto(
    int InventoryItemId,
    DateOnly? LastMaintainedDate,
    int IntervalValue,
    MaintenanceIntervalUnit IntervalUnit,
    string? Notes,
    DateOnly? NextDueDate,
    bool IsOverdue
);

public record MaintenanceVisitDto(
    int Id,
    int InventoryItemId,
    int Quantity,
    int? VendorId,
    string? VendorName,
    DateTime OutForMaintenanceAt,
    DateTime? ReturnedAt,
    string? Notes,
    bool IsOut
);

// Everything the item view modal needs for the Maintenance tab: the schedule (may be null) and
// the currently-open visit (null when the item is not out for maintenance).
public record MaintenanceItemStatusDto(
    int InventoryItemId,
    MaintenanceScheduleDto? Schedule,
    MaintenanceVisitDto? OpenVisit
);

public record MarkOutForMaintenanceDto(
    int InventoryItemId,
    int Quantity,
    int? VendorId,
    string? Notes
);

public record SaveMaintenanceScheduleDto(
    int InventoryItemId,
    DateOnly? LastMaintainedDate,
    int IntervalValue,
    MaintenanceIntervalUnit IntervalUnit,
    string? Notes
);

// ── Maintenance Due report ────────────────────────────────────────────────────
public record MaintenanceReportDto(
    IReadOnlyList<MaintenanceReportRow> Items,
    int OverdueCount,
    int DueSoonCount,
    int OutCount,
    DateTime GeneratedAt
);

public record MaintenanceReportRow(
    int InventoryItemId,
    string Name,
    string? SKU,
    string? Location,
    DateOnly? LastMaintainedDate,
    DateOnly? NextDueDate,
    bool IsOverdue,
    bool IsDueSoon,
    bool IsOut,
    string? VendorName
);
