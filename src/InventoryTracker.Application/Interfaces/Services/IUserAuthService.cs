using InventoryTracker.Application.DTOs;
using InventoryTracker.Domain.Entities;

namespace InventoryTracker.Application.Interfaces.Services;

public interface IUserAuthService
{
    Task<bool> IsSetupRequiredAsync();
    Task<User> SetupAdminAsync(string username, string? email, string password,
        string? firstName = null, string? lastName = null);
    Task<User?> ValidateCredentialsAsync(string username, string password);
    Task<string> ResetAdminPasswordAsync(string newPassword);
    Task<UserDto?> GetUserAsync(int id);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto> CreateUserAsync(CreateUserDto dto);
    Task UpdateUserAsync(int id, UpdateUserDto dto);
    Task ResetUserPasswordAsync(int id, string newPassword);
    Task SetUserSuspendedAsync(int id, bool suspended);
    Task DeleteUserAsync(int id);
}
