using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class SafetyDataSheetRepository : ISafetyDataSheetRepository
{
    private readonly AppDbContext _context;

    public SafetyDataSheetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SafetyDataSheet>> GetByInventoryItemIdAsync(int inventoryItemId) =>
        await _context.SafetyDataSheets
            .Where(s => s.InventoryItemId == inventoryItemId)
            .OrderBy(s => s.Source)
            .ToListAsync();

    public async Task<IEnumerable<SafetyDataSheet>> GetAllAsync() =>
        await _context.SafetyDataSheets
            .OrderByDescending(s => s.FetchedAt)
            .ToListAsync();

    public async Task<SafetyDataSheet?> GetByIdAsync(int id) =>
        await _context.SafetyDataSheets.FindAsync(id);

    public async Task AddRangeAsync(IEnumerable<SafetyDataSheet> sheets)
    {
        _context.SafetyDataSheets.AddRange(sheets);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByInventoryItemIdAsync(int inventoryItemId)
    {
        var existing = await _context.SafetyDataSheets
            .Where(s => s.InventoryItemId == inventoryItemId)
            .ToListAsync();
        if (existing.Count > 0)
        {
            _context.SafetyDataSheets.RemoveRange(existing);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        var row = await _context.SafetyDataSheets.FindAsync(id);
        if (row is not null)
        {
            _context.SafetyDataSheets.Remove(row);
            await _context.SaveChangesAsync();
        }
    }
}
