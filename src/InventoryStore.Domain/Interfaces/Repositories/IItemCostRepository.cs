using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IItemCostRepository
{
    Task<ItemCost?> GetByInventoryItemIdAsync(int inventoryItemId);
    Task<IEnumerable<ItemCost>> GetAllAsync();
    Task UpsertAsync(int inventoryItemId, decimal unitCost, DateOnly? purchaseDate, int? usefulLifeMonths);
    Task DeleteByInventoryItemIdAsync(int inventoryItemId);
}
