using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _repo;

    public TagService(ITagRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<TagDto>> GetAllAsync()
        => (await _repo.GetAllAsync()).Select(ToDto);

    public async Task<TagDto?> GetByIdAsync(int id)
    {
        var t = await _repo.GetByIdAsync(id);
        return t is null ? null : ToDto(t);
    }

    public async Task<TagDto> CreateAsync(CreateTagDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Tag name is required.");

        var existing = await _repo.GetByNameAsync(dto.Name.Trim());
        if (existing is not null)
            throw new InvalidOperationException($"A tag named '{dto.Name.Trim()}' already exists.");

        var created = await _repo.CreateAsync(new Tag { Name = dto.Name.Trim() });
        return ToDto(created);
    }

    public async Task UpdateAsync(int id, UpdateTagDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Tag name is required.");

        var tag = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Tag {id} not found.");

        var existing = await _repo.GetByNameAsync(dto.Name.Trim());
        if (existing is not null && existing.Id != id)
            throw new InvalidOperationException($"A tag named '{dto.Name.Trim()}' already exists.");

        tag.Name = dto.Name.Trim();
        await _repo.UpdateAsync(tag);
    }

    public async Task DeleteAsync(int id)
        => await _repo.DeleteAsync(id);

    private static TagDto ToDto(Tag t) => new(t.Id, t.Name);
}
