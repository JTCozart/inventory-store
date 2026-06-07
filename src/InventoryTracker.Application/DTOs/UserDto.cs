using InventoryTracker.Domain.Enums;

namespace InventoryTracker.Application.DTOs;

public record UserDto(
    int Id,
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt
)
{
    public string DisplayName => !string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName)
        ? $"{FirstName} {LastName}".Trim()
        : Username;
}

public record CreateUserDto(
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    string Password,
    UserRole Role
);

public record UpdateUserDto(
    string? FirstName,
    string? LastName,
    string? Email,
    UserRole Role,
    bool IsActive
);
