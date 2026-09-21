using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _context;

    public ApiKeyRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<ApiKey>> GetAllAsync() =>
        await _context.ApiKeys.AsNoTracking().OrderBy(k => k.Id).ToListAsync();

    public async Task<ApiKey?> GetByHashAsync(string keyHash)
    {
        var now = DateTime.UtcNow;
        return await _context.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k =>
            k.KeyHash == keyHash
            || (k.PreviousKeyHash == keyHash && k.PreviousKeyExpiresAt != null && k.PreviousKeyExpiresAt > now));
    }

    public async Task<ApiKey> CreateAsync(ApiKey key)
    {
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();
        return key;
    }

    public async Task SetEnabledAsync(int id, bool enabled) =>
        await _context.ApiKeys.Where(k => k.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.Enabled, enabled));

    public async Task<bool> RotateAsync(int id, string newHash, string newPrefix, TimeSpan grace)
    {
        var key = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id);
        if (key is null) return false;

        var now = DateTime.UtcNow;
        if (grace > TimeSpan.Zero)
        {
            key.PreviousKeyHash      = key.KeyHash;
            key.PreviousKeyExpiresAt = now + grace;
        }
        else
        {
            key.PreviousKeyHash      = null;
            key.PreviousKeyExpiresAt = null;
        }
        key.KeyHash   = newHash;
        key.Prefix    = newPrefix;
        key.RotatedAt = now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task DeleteAsync(int id) =>
        await _context.ApiKeys.Where(k => k.Id == id).ExecuteDeleteAsync();

    public async Task TouchLastUsedAsync(int id)
    {
        var now = DateTime.UtcNow;
        await _context.ApiKeys.Where(k => k.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now));
    }
}
