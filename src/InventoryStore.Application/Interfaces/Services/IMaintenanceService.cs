using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface IMaintenanceService
{
    Task<MaintenanceItemStatusDto> GetItemStatusAsync(int inventoryItemId);
    Task SaveScheduleAsync(SaveMaintenanceScheduleDto dto, int userId, string username);
    Task MarkOutAsync(MarkOutForMaintenanceDto dto, int userId, string username);
    Task ReturnAsync(int inventoryItemId, string? notes, int userId, string username);
}
