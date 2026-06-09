using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repo;

    public CategoryService(ICategoryRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
        => (await _repo.GetAllAsync()).Select(ToDto);

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var c = await _repo.GetByIdAsync(id);
        return c is null ? null : ToDto(c);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Category name is required.");

        var existing = await _repo.GetByNameAsync(dto.Name.Trim());
        if (existing is not null)
            throw new InvalidOperationException($"A category named '{dto.Name.Trim()}' already exists.");

        var created = await _repo.CreateAsync(new Category
        {
            Name  = dto.Name.Trim(),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim()
        });
        return ToDto(created);
    }

    public async Task UpdateAsync(int id, UpdateCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Category name is required.");

        var category = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Category {id} not found.");

        var existing = await _repo.GetByNameAsync(dto.Name.Trim());
        if (existing is not null && existing.Id != id)
            throw new InvalidOperationException($"A category named '{dto.Name.Trim()}' already exists.");

        category.Name  = dto.Name.Trim();
        category.Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim();
        await _repo.UpdateAsync(category);
    }

    public async Task DeleteAsync(int id)
        => await _repo.DeleteAsync(id);

    private static CategoryDto ToDto(Category c) => new(c.Id, c.Name, c.Color);
}
