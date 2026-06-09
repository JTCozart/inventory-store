using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Entities;
using InventoryStore.Domain.Enums;
using InventoryStore.Domain.Interfaces.Repositories;

namespace InventoryStore.Application.Services;

public class AuthenticationService : IUserAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AuthenticationService(
        IUserRepository userRepository,
        IActivityLogRepository activityLogRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _activityLogRepository = activityLogRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> IsSetupRequiredAsync() => !await _userRepository.AnyAsync();

    public async Task<User> SetupAdminAsync(string username, string? email, string password,
        string? firstName = null, string? lastName = null)
    {
        var admin = new User
        {
            Username = username.Trim(),
            FirstName = firstName?.Trim(),
            LastName = lastName?.Trim(),
            Email = email?.Trim(),
            PasswordHash = _passwordHasher.Hash(password),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userRepository.CreateAsync(admin);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = created.Id,
            Username = created.Username,
            Action = "System",
            Details = "Initial admin account created"
        });

        return created;
    }

    public async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _userRepository.GetByUsernameAsync(username);
        if (user is null || !user.IsActive || !_passwordHasher.Verify(password, user.PasswordHash))
            return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "Login",
            Details = "User logged in"
        });

        return user;
    }

    public async Task<string> ResetAdminPasswordAsync(string newPassword)
    {
        var admin = await _userRepository.GetAdminAsync()
            ?? throw new InvalidOperationException("No admin account found.");

        admin.PasswordHash = _passwordHasher.Hash(newPassword);
        await _userRepository.UpdateAsync(admin);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = admin.Id,
            Username = admin.Username,
            Action = "PasswordReset",
            Details = "Admin password reset via system tray"
        });

        return admin.Username;
    }

    public async Task<UserDto?> GetUserAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user is null ? null : MapToDto(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToDto);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        dto = dto with { Username = dto.Username.Trim() };
        var existing = await _userRepository.GetByUsernameAsync(dto.Username);
        if (existing is not null)
            throw new InvalidOperationException($"Username '{dto.Username}' is already taken.");

        var user = new User
        {
            Username = dto.Username,
            FirstName = dto.FirstName?.Trim(),
            LastName = dto.LastName?.Trim(),
            Email = dto.Email?.Trim(),
            PasswordHash = _passwordHasher.Hash(dto.Password),
            Role = dto.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userRepository.CreateAsync(user);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = created.Id,
            Username = created.Username,
            Action = "Created",
            EntityType = "User",
            EntityId = created.Id,
            Details = $"User '{created.Username}' created"
        });

        return MapToDto(created);
    }

    public async Task UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");

        user.FirstName = dto.FirstName?.Trim();
        user.LastName = dto.LastName?.Trim();
        user.Email = dto.Email?.Trim();
        user.Role = dto.Role;
        user.IsActive = dto.IsActive;
        await _userRepository.UpdateAsync(user);
    }

    public async Task ResetUserPasswordAsync(int id, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        await _userRepository.UpdateAsync(user);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "PasswordReset",
            Details = $"Password reset for user '{user.Username}'"
        });
    }

    public async Task SetUserSuspendedAsync(int id, bool suspended)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");


        user.IsActive = !suspended;
        await _userRepository.UpdateAsync(user);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = suspended ? "Suspended" : "Unsuspended",
            EntityType = "User",
            EntityId = user.Id,
            Details = $"User '{user.Username}' {(suspended ? "suspended" : "reactivated")}"
        });
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");


        await _userRepository.DeleteAsync(id);
    }

    private static UserDto MapToDto(User user) => new(
        user.Id,
        user.Username,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role,
        user.IsActive,
        user.CreatedAt,
        user.LastLoginAt
    );
}
