namespace InventoryStore.Application.DTOs;

public record VendorDto(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateVendorDto(
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    string? Notes
);

public record UpdateVendorDto(
    string Name,
    string? Phone,
    string? Email,
    string? Address,
    string? Notes
);
