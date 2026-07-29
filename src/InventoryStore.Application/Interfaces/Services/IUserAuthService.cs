using InventoryStore.Application.DTOs;
using InventoryStore.Domain.Entities;

namespace InventoryStore.Application.Interfaces.Services;

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

    // Accepts either a username or an email address. Enumeration-safe: does nothing observable
    // to the caller whether or not the account exists or has an email on file. Sends a reset
    // link when it does.
    Task RequestPasswordResetAsync(string usernameOrEmail, string resetBaseUrl);

    // Returns false for an invalid, expired, or already-used token.
    Task<bool> ResetPasswordWithTokenAsync(string token, string newPassword);
}
