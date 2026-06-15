using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IMaintenanceRepository
{
    // Per-item schedule (1:1).
    Task<MaintenanceSchedule?> GetScheduleAsync(int inventoryItemId);
    Task<MaintenanceSchedule> UpsertScheduleAsync(MaintenanceSchedule schedule);

    // Out-for-maintenance visits.
    Task<MaintenanceVisit?> GetOpenVisitAsync(int inventoryItemId);
    Task<MaintenanceVisit?> GetVisitAsync(int visitId);
    Task<IEnumerable<MaintenanceVisit>> GetVisitsByItemAsync(int inventoryItemId);
    Task<MaintenanceVisit> CreateVisitAsync(MaintenanceVisit visit);
    Task UpdateVisitAsync(MaintenanceVisit visit);

    // All schedules + open visits, for the Maintenance Due report.
    Task<IReadOnlyList<MaintenanceSchedule>> GetAllSchedulesAsync();
    Task<IReadOnlyList<MaintenanceVisit>> GetAllOpenVisitsAsync();
}
