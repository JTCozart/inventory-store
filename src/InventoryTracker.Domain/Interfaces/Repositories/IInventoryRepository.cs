using InventoryTracker.Domain.Entities;

namespace InventoryTracker.Domain.Interfaces.Repositories;

public interface IInventoryRepository
{
    Task<IEnumerable<InventoryItem>> GetAllAsync();
    Task<InventoryItem?> GetByIdAsync(int id);
    Task<IEnumerable<InventoryItem>> SearchAsync(string query);
    Task<IEnumerable<InventoryItem>> GetLowStockAsync();
    Task<InventoryItem> CreateAsync(InventoryItem item);
    Task UpdateAsync(InventoryItem item);
    Task DeleteAsync(int id);
    Task<int> CountAsync();
    Task<int> TotalQuantityAsync();
}
