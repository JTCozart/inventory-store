namespace InventoryStore.Application.DTOs;

public record ClientDto(
    int Id,
    string FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string? Address,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string DisplayName
);

public record CreateClientDto(
    string FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string? Address,
    string? Notes
);

public record UpdateClientDto(
    string FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string? Address,
    string? Notes
);
