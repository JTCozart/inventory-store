using InventoryStore.Domain.Enums;

namespace InventoryStore.Application.DTOs;

public record UserDto(
    int Id,
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    // True for the locked first-admin account when the instance runs in professional-services
    // hosted mode. Such accounts are presented as a SYSTEM account and cannot be managed.
    bool IsSystemLocked = false
)
{
    // The locked system account is shown as "SYSTEM USER" regardless of its real name, and as
    // "SYSTEM" where the login name would appear.
    public const string SystemDisplayName = "SYSTEM USER";
    public const string SystemUsername = "SYSTEM";

    public string DisplayName => IsSystemLocked
        ? SystemDisplayName
        : !string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName)
            ? $"{FirstName} {LastName}".Trim()
            : Username;

    // Login name to show in the UI (real name hidden for the locked system account).
    public string DisplayUsername => IsSystemLocked ? SystemUsername : Username;

    // Email to show in the UI (hidden for the locked system account).
    public string? DisplayEmail => IsSystemLocked ? null : Email;
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
