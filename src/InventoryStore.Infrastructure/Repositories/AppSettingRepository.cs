using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class AppSettingRepository : IAppSettingRepository
{
    private readonly AppDbContext _context;

    public AppSettingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AppSetting?> GetByKeyAsync(string key) =>
        await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);

    public async Task<string?> GetValueAsync(string key)
    {
        var setting = await GetByKeyAsync(key);
        return setting?.Value;
    }

    public async Task SetValueAsync(string key, string? value)
    {
        var existing = await GetByKeyAsync(key);
        if (existing is null)
        {
            _context.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.AppSettings.Update(existing);
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AppSetting>> GetAllAsync() =>
        await _context.AppSettings.OrderBy(s => s.Key).ToListAsync();
}
