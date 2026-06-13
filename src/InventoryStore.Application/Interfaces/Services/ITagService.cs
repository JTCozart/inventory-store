using InventoryStore.Application.DTOs;

namespace InventoryStore.Application.Interfaces.Services;

public interface ITagService
{
    Task<IEnumerable<TagDto>> GetAllAsync();
    Task<TagDto?> GetByIdAsync(int id);
    Task<TagDto> CreateAsync(CreateTagDto dto);
    Task UpdateAsync(int id, UpdateTagDto dto);
    Task DeleteAsync(int id);
}
