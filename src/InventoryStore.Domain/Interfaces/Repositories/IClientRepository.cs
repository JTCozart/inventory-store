using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface IClientRepository
{
    Task<IEnumerable<Client>> GetAllAsync();
    Task<Client?> GetByIdAsync(int id);
    Task<IEnumerable<Client>> SearchAsync(string query);
    Task<Client?> GetByNameAsync(string firstName, string? lastName);
    Task<Client> CreateAsync(Client client);
    Task UpdateAsync(Client client);
    Task DeleteAsync(int id);
}
