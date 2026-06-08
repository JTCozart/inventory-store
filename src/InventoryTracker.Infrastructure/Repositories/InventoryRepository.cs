using InventoryTracker.Domain.Entities;
using InventoryTracker.Domain.Interfaces.Repositories;
using InventoryTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Infrastructure.Repositories;

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
}
