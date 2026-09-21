using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IApiKeyRepository
{
    Task<IEnumerable<ApiKey>> GetAllAsync();
    // Matches the current key hash, or a previous one still inside its rotation grace window.
    Task<ApiKey?> GetByHashAsync(string keyHash);
    Task<ApiKey> CreateAsync(ApiKey key);
    Task SetEnabledAsync(int id, bool enabled);
    // Returns false when the key does not exist.
    Task<bool> RotateAsync(int id, string newHash, string newPrefix, TimeSpan grace);
    Task DeleteAsync(int id);
    Task TouchLastUsedAsync(int id);
}
