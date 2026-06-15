using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;
using InventoryStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryStore.Infrastructure.Repositories;

public class VendorRepository : IVendorRepository
{
    private readonly AppDbContext _context;

    public VendorRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Vendor>> GetAllAsync()
        => await _context.Vendors.OrderBy(v => v.Name).ToListAsync();

    public async Task<Vendor?> GetByIdAsync(int id)
        => await _context.Vendors.FindAsync(id);

    public async Task<IEnumerable<Vendor>> SearchAsync(string query)
    {
        var lower = query.ToLower();
        return await _context.Vendors
            .Where(v => v.Name.ToLower().Contains(lower)
                     || (v.Phone != null && v.Phone.Contains(lower))
                     || (v.Email != null && v.Email.ToLower().Contains(lower)))
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task<Vendor?> GetByNameAsync(string name)
    {
        var lower = name.ToLower();
        return await _context.Vendors.FirstOrDefaultAsync(v => v.Name.ToLower() == lower);
    }

    public async Task<Vendor> CreateAsync(Vendor vendor)
    {
        _context.Vendors.Add(vendor);
        await _context.SaveChangesAsync();
        return vendor;
    }

    public async Task UpdateAsync(Vendor vendor)
    {
        _context.Vendors.Update(vendor);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var v = await _context.Vendors.FindAsync(id);
        if (v is not null) { _context.Vendors.Remove(v); await _context.SaveChangesAsync(); }
    }
}
