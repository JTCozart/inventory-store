using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface IVendorService
{
    Task<IEnumerable<VendorDto>> GetAllAsync();
    Task<VendorDto?> GetByIdAsync(int id);
    Task<IEnumerable<VendorDto>> SearchAsync(string query);
    Task<VendorDto> CreateAsync(CreateVendorDto dto);
    Task<VendorDto> QuickCreateAsync(string name);
    Task UpdateAsync(int id, UpdateVendorDto dto);
    Task DeleteAsync(int id);
}
