using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly IAppSettingRepository _settingRepository;

    public SettingsService(IAppSettingRepository settingRepository)
    {
        _settingRepository = settingRepository;
    }

    public Task<string?> GetAsync(string key) => _settingRepository.GetValueAsync(key);

    public Task SetAsync(string key, string? value) => _settingRepository.SetValueAsync(key, value);

    public async Task<Dictionary<string, string?>> GetAllAsync()
    {
        var settings = await _settingRepository.GetAllAsync();
        return settings.ToDictionary(s => s.Key, s => s.Value);
    }
}
