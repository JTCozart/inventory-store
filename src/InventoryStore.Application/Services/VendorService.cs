using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class VendorService : IVendorService
{
    private readonly IVendorRepository _repo;

    public VendorService(IVendorRepository repo) => _repo = repo;

    public async Task<IEnumerable<VendorDto>> GetAllAsync()
        => (await _repo.GetAllAsync()).Select(Map);

    public async Task<VendorDto?> GetByIdAsync(int id)
    {
        var v = await _repo.GetByIdAsync(id);
        return v is null ? null : Map(v);
    }

    public async Task<IEnumerable<VendorDto>> SearchAsync(string query)
        => (await _repo.SearchAsync(query)).Select(Map);

    public async Task<VendorDto> CreateAsync(CreateVendorDto dto)
    {
        var vendor = new Vendor
        {
            Name      = dto.Name.Trim(),
            Phone     = dto.Phone?.Trim(),
            Email     = dto.Email?.Trim(),
            Address   = dto.Address?.Trim(),
            Notes     = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return Map(await _repo.CreateAsync(vendor));
    }

    public async Task<VendorDto> QuickCreateAsync(string name)
    {
        var trimmed = name.Trim();

        // Reuse an existing vendor when the name matches (case-insensitive) so that
        // "Acme", "acme" and "ACME" all link to the same vendor instead of duplicating.
        var existing = await _repo.GetByNameAsync(trimmed);
        if (existing is not null)
            return Map(existing);

        var vendor = new Vendor
        {
            Name      = trimmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return Map(await _repo.CreateAsync(vendor));
    }

    public async Task UpdateAsync(int id, UpdateVendorDto dto)
    {
        var vendor = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Vendor {id} not found.");
        vendor.Name      = dto.Name.Trim();
        vendor.Phone     = dto.Phone?.Trim();
        vendor.Email     = dto.Email?.Trim();
        vendor.Address   = dto.Address?.Trim();
        vendor.Notes     = dto.Notes?.Trim();
        vendor.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(vendor);
    }

    public async Task DeleteAsync(int id) => await _repo.DeleteAsync(id);

    private static VendorDto Map(Vendor v) => new(
        v.Id, v.Name, v.Phone, v.Email, v.Address, v.Notes, v.CreatedAt, v.UpdatedAt);
}
