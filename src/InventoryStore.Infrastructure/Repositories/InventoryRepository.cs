using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _context;

    public InventoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<InventoryItem>> GetAllAsync() =>
        await _context.InventoryItems
            .Include(i => i.Category)
            .OrderBy(i => i.Name)
            .ToListAsync();

    public async Task<InventoryItem?> GetByIdAsync(int id) =>
        await _context.InventoryItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IEnumerable<InventoryItem>> SearchAsync(string query)
    {
        var lower = query.ToLower();
        return await _context.InventoryItems
            .Include(i => i.Category)
            .Where(i => i.Name.ToLower().Contains(lower)
                     || (i.SKU != null && i.SKU.ToLower().Contains(lower))
                     || (i.Location != null && i.Location.ToLower().Contains(lower))
                     || (i.Description != null && i.Description.ToLower().Contains(lower))
                     || (i.Category != null && i.Category.Name.ToLower().Contains(lower)))
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetLowStockAsync()
    {
        var all = await _context.InventoryItems.Include(i => i.Category).ToListAsync();
        return all
            .Where(i => i.IsLowStock)
            .OrderBy(i => i.AvailableQuantity);
    }

    public async Task<IEnumerable<InventoryItem>> GetPublicAsync() =>
        await _context.InventoryItems
            .Include(i => i.Category)
            .Where(i => i.IsPublic)
            .OrderBy(i => i.Name)
            .ToListAsync();

    public async Task<InventoryItem> CreateAsync(InventoryItem item)
    {
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(InventoryItem item)
    {
        _context.InventoryItems.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item is not null)
        {
            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }

    public Task<int> CountAsync() => _context.InventoryItems.CountAsync();

    public async Task<int> TotalQuantityAsync() =>
        await _context.InventoryItems.SumAsync(i => (int?)i.Quantity) ?? 0;

    public async Task<IEnumerable<(string Location, int Count)>> GetAllLocationsAsync()
    {
        var rows = await _context.InventoryItems
            .Where(i => i.Location != null && i.Location != "")
            .GroupBy(i => i.Location!)
            .Select(g => new { Location = g.Key, Count = g.Count() })
            .OrderBy(x => x.Location)
            .ToListAsync();
        return rows.Select(r => (r.Location, r.Count));
    }

    public async Task BulkUpdateLocationAsync(string from, string? to)
    {
        await _context.InventoryItems
            .Where(i => i.Location == from)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Location, to));
    }
}
