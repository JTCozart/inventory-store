using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class CheckoutRepository : ICheckoutRepository
{
    private readonly AppDbContext _context;

    public CheckoutRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CheckoutRecord> CreateAsync(CheckoutRecord record)
    {
        _context.CheckoutRecords.Add(record);
        await _context.SaveChangesAsync();
        return record;
    }

    public async Task<CheckoutRecord?> GetByIdAsync(int id) =>
        await _context.CheckoutRecords.FindAsync(id);

    public async Task UpdateAsync(CheckoutRecord record)
    {
        _context.CheckoutRecords.Update(record);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<CheckoutRecord>> GetActiveByItemAsync(int inventoryItemId) =>
        await _context.CheckoutRecords
            .Where(r => r.InventoryItemId == inventoryItemId && r.CheckedInAt == null && !r.IsLost)
            .OrderByDescending(r => r.CheckedOutAt)
            .ToListAsync();

    public async Task<IEnumerable<CheckoutRecord>> GetLostByItemAsync(int inventoryItemId) =>
        await _context.CheckoutRecords
            .Where(r => r.InventoryItemId == inventoryItemId && r.IsLost)
            .OrderByDescending(r => r.CheckedInAt)
            .ToListAsync();

    public async Task<IEnumerable<CheckoutRecord>> GetAllByItemAsync(int inventoryItemId) =>
        await _context.CheckoutRecords
            .Where(r => r.InventoryItemId == inventoryItemId)
            .OrderByDescending(r => r.CheckedOutAt)
            .ToListAsync();

    public async Task<IEnumerable<CheckoutRecord>> GetAllActiveAsync() =>
        await _context.CheckoutRecords
            .Where(r => r.CheckedInAt == null && !r.IsLost)
            .OrderByDescending(r => r.CheckedOutAt)
            .ToListAsync();

    public async Task<IEnumerable<CheckoutRecord>> GetAllLostAsync() =>
        await _context.CheckoutRecords
            .Where(r => r.IsLost)
            .OrderByDescending(r => r.CheckedInAt)
            .ToListAsync();

    public async Task<IEnumerable<CheckoutRecord>> GetByClientIdAsync(int clientId) =>
        await _context.CheckoutRecords
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.CheckedOutAt)
            .ToListAsync();

    public async Task DeleteByInventoryItemIdAsync(int inventoryItemId) =>
        await _context.CheckoutRecords
            .Where(r => r.InventoryItemId == inventoryItemId)
            .ExecuteDeleteAsync();
}
