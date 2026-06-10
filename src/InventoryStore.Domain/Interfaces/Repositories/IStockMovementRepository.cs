using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IStockMovementRepository
{
    Task AddAsync(StockMovement movement);
    Task<IEnumerable<StockMovement>> GetByInventoryItemIdAsync(int inventoryItemId);
    // Consume movements at or after the cutoff, used to compute average daily usage.
    Task<IEnumerable<StockMovement>> GetConsumeSinceAsync(DateTime cutoffUtc);
}
