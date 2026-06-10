using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class ItemCostRepository : IItemCostRepository
{
    private readonly AppDbContext _context;

    public ItemCostRepository(AppDbContext context) => _context = context;

    public async Task<ItemCost?> GetByInventoryItemIdAsync(int inventoryItemId) =>
        await _context.ItemCosts.FirstOrDefaultAsync(c => c.InventoryItemId == inventoryItemId);

    public async Task<IEnumerable<ItemCost>> GetAllAsync() =>
        await _context.ItemCosts.ToListAsync();

    public async Task UpsertAsync(int inventoryItemId, decimal unitCost, DateOnly? purchaseDate, int? usefulLifeMonths)
    {
        var existing = await _context.ItemCosts.FirstOrDefaultAsync(c => c.InventoryItemId == inventoryItemId);
        if (existing is null)
        {
            _context.ItemCosts.Add(new ItemCost
            {
                InventoryItemId = inventoryItemId,
                UnitCost = unitCost,
                PurchaseDate = purchaseDate,
                UsefulLifeMonths = usefulLifeMonths,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.UnitCost = unitCost;
            existing.PurchaseDate = purchaseDate;
            existing.UsefulLifeMonths = usefulLifeMonths;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByInventoryItemIdAsync(int inventoryItemId)
    {
        var existing = await _context.ItemCosts.FirstOrDefaultAsync(c => c.InventoryItemId == inventoryItemId);
        if (existing is not null)
        {
            _context.ItemCosts.Remove(existing);
            await _context.SaveChangesAsync();
        }
    }
}
