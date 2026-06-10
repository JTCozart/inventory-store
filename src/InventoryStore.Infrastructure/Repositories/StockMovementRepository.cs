using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly AppDbContext _context;

    public StockMovementRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(StockMovement movement)
    {
        _context.StockMovements.Add(movement);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetByInventoryItemIdAsync(int inventoryItemId) =>
        await _context.StockMovements
            .Where(m => m.InventoryItemId == inventoryItemId)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

    public async Task<IEnumerable<StockMovement>> GetConsumeSinceAsync(DateTime cutoffUtc) =>
        await _context.StockMovements
            .Where(m => m.ChangeType == "Consume" && m.Timestamp >= cutoffUtc)
            .ToListAsync();
}
