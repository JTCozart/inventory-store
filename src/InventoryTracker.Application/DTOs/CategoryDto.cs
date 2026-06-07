namespace InventoryTracker.Application.DTOs;

public record CategoryDto(int Id, string Name, string? Color);
public record CreateCategoryDto(string Name, string? Color);
public record UpdateCategoryDto(string Name, string? Color);
