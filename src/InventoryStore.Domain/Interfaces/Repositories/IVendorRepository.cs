using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IVendorRepository
{
    Task<IEnumerable<Vendor>> GetAllAsync();
    Task<Vendor?> GetByIdAsync(int id);
    Task<IEnumerable<Vendor>> SearchAsync(string query);
    Task<Vendor?> GetByNameAsync(string name);
    Task<Vendor> CreateAsync(Vendor vendor);
    Task UpdateAsync(Vendor vendor);
    Task DeleteAsync(int id);
}
