using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IAppSettingRepository
{
    Task<AppSetting?> GetByKeyAsync(string key);
    Task<string?> GetValueAsync(string key);
    Task SetValueAsync(string key, string? value);
    Task<IEnumerable<AppSetting>> GetAllAsync();
}
