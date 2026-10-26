using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Tests.Fakes;

// Name lookup is case-insensitive, like the real repository, so tests can prove a vendor is reused despite its casing.
public class FakeVendorRepository : IVendorRepository
{
    private int _nextId = 1;

    public List<Vendor> Vendors { get; } = new();
    public int CreateCallCount { get; private set; }

    public Task<IEnumerable<Vendor>> GetAllAsync() => Task.FromResult<IEnumerable<Vendor>>(Vendors.ToList());

    public Task<Vendor?> GetByIdAsync(int id) => Task.FromResult(Vendors.FirstOrDefault(v => v.Id == id));

    public Task<IEnumerable<Vendor>> SearchAsync(string query) =>
        Task.FromResult<IEnumerable<Vendor>>(
            Vendors.Where(v => v.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());

    public Task<Vendor?> GetByNameAsync(string name) =>
        Task.FromResult(Vendors.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<Vendor> CreateAsync(Vendor vendor)
    {
        CreateCallCount++;
        vendor.Id = _nextId++;
        Vendors.Add(vendor);
        return Task.FromResult(vendor);
    }

    public Task UpdateAsync(Vendor vendor) => Task.CompletedTask;

    public Task DeleteAsync(int id)
    {
        Vendors.RemoveAll(v => v.Id == id);
        return Task.CompletedTask;
    }
}
