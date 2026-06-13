namespace InventoryStore.Application.DTOs;

public record TagDto(int Id, string Name);
public record CreateTagDto(string Name);
public record UpdateTagDto(string Name);
