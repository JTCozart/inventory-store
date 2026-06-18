using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class KitRepository : IKitRepository
{
    private readonly AppDbContext _context;

    public KitRepository(AppDbContext context)
    {
        _context = context;
    }

    // ── Components ─────────────────────────────────────────────────────
    public async Task<KitComponent?> GetComponentAsync(int kitItemId, int componentItemId) =>
        await _context.KitComponents
            .FirstOrDefaultAsync(c => c.KitItemId == kitItemId && c.ComponentItemId == componentItemId);

    public async Task AddComponentAsync(KitComponent component)
    {
        _context.KitComponents.Add(component);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateComponentAsync(KitComponent component)
    {
        _context.KitComponents.Update(component);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveComponentAsync(int kitItemId, int componentItemId) =>
        await _context.KitComponents
            .Where(c => c.KitItemId == kitItemId && c.ComponentItemId == componentItemId)
            .ExecuteDeleteAsync();

    public async Task RemoveComponentsReferencingItemAsync(int componentItemId) =>
        await _context.KitComponents
            .Where(c => c.ComponentItemId == componentItemId)
            .ExecuteDeleteAsync();

    // ── Kit checkouts ──────────────────────────────────────────────────
    public async Task<KitCheckout> CreateCheckoutAsync(KitCheckout checkout)
    {
        _context.KitCheckouts.Add(checkout);
        await _context.SaveChangesAsync();
        return checkout;
    }

    public async Task<KitCheckout?> GetCheckoutAsync(int id) =>
        await _context.KitCheckouts
            .Include(k => k.ComponentCheckouts)
            .Include(k => k.ConsumableAllocations)
            .FirstOrDefaultAsync(k => k.Id == id);

    public async Task UpdateCheckoutAsync(KitCheckout checkout)
    {
        _context.KitCheckouts.Update(checkout);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<KitCheckout>> GetActiveByKitAsync(int kitItemId) =>
        await _context.KitCheckouts
            .Include(k => k.ComponentCheckouts)
            .Where(k => k.KitItemId == kitItemId && k.CheckedInAt == null && !k.IsLost)
            .OrderByDescending(k => k.CheckedOutAt)
            .ToListAsync();

    public async Task<IEnumerable<KitCheckout>> GetAllActiveAsync() =>
        await _context.KitCheckouts
            .Include(k => k.ComponentCheckouts)
            .Where(k => k.CheckedInAt == null && !k.IsLost)
            .OrderByDescending(k => k.CheckedOutAt)
            .ToListAsync();

    public async Task DeleteByKitItemIdAsync(int kitItemId) =>
        await _context.KitCheckouts
            .Where(k => k.KitItemId == kitItemId)
            .ExecuteDeleteAsync();

    // ── Consumable reconciliation ──────────────────────────────────────
    public async Task AddAllocationAsync(KitConsumableAllocation allocation)
    {
        _context.KitConsumableAllocations.Add(allocation);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAllocationAsync(KitConsumableAllocation allocation)
    {
        _context.KitConsumableAllocations.Update(allocation);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<KitCheckout>> GetPendingReconciliationsAsync() =>
        await _context.KitCheckouts
            .Include(k => k.ConsumableAllocations)
            .Where(k => k.NeedsReconciliation)
            .OrderByDescending(k => k.CheckedInAt)
            .ToListAsync();
}
