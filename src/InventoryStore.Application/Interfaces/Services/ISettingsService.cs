namespace InventoryStore.Application.Interfaces.Services;

public interface ISettingsService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string? value);
    Task<Dictionary<string, string?>> GetAllAsync();
}
