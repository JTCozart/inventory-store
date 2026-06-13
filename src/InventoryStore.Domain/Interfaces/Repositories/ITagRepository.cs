using InventoryStore.Domain.Entities;

namespace InventoryStore.Domain.Interfaces.Repositories;

public interface ITagRepository
{
    Task<IEnumerable<Tag>> GetAllAsync();
    Task<Tag?> GetByIdAsync(int id);
    Task<Tag?> GetByNameAsync(string name);
    Task<Tag> CreateAsync(Tag tag);
    Task UpdateAsync(Tag tag);
    Task DeleteAsync(int id);

    // Resolves the given names to existing tags (case-insensitive), creating any that are missing.
    Task<List<Tag>> GetOrCreateByNamesAsync(IEnumerable<string> names);
}
