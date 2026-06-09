using InventoryTracker.Domain.Entities;

namespace InventoryTracker.Domain.Interfaces.Repositories;

public interface ICheckoutRepository
{
    Task<CheckoutRecord> CreateAsync(CheckoutRecord record);
    Task<CheckoutRecord?> GetByIdAsync(int id);
    Task UpdateAsync(CheckoutRecord record);
    Task<IEnumerable<CheckoutRecord>> GetActiveByItemAsync(int inventoryItemId);
    Task<IEnumerable<CheckoutRecord>> GetLostByItemAsync(int inventoryItemId);
    Task<IEnumerable<CheckoutRecord>> GetAllByItemAsync(int inventoryItemId);
    Task<IEnumerable<CheckoutRecord>> GetAllActiveAsync();
    Task<IEnumerable<CheckoutRecord>> GetAllLostAsync();
    Task<IEnumerable<CheckoutRecord>> GetByClientIdAsync(int clientId);
}
