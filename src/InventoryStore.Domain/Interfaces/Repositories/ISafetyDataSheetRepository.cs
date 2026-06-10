using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface ISafetyDataSheetRepository
{
    Task<IEnumerable<SafetyDataSheet>> GetByInventoryItemIdAsync(int inventoryItemId);
    Task<IEnumerable<SafetyDataSheet>> GetAllAsync();
    Task<SafetyDataSheet?> GetByIdAsync(int id);
    Task AddRangeAsync(IEnumerable<SafetyDataSheet> sheets);
    Task DeleteByInventoryItemIdAsync(int inventoryItemId);
    Task DeleteAsync(int id);
}
