using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface IClientService
{
    Task<IEnumerable<ClientDto>> GetAllAsync();
    Task<ClientDto?> GetByIdAsync(int id);
    Task<IEnumerable<ClientDto>> SearchAsync(string query);
    Task<ClientDto> CreateAsync(CreateClientDto dto);
    Task<ClientDto> QuickCreateAsync(string name);
    Task UpdateAsync(int id, UpdateClientDto dto);
    Task DeleteAsync(int id);
}
