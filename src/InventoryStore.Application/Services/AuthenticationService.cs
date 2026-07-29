using System.Security.Cryptography;
using System.Text;
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
    private readonly IHostingMode _hostingMode;
    private readonly IPasswordResetTokenRepository _resetTokenRepository;
    private readonly IEmailSender _emailSender;
    private readonly ISettingsService _settingsService;

    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    public AuthenticationService(
        IUserRepository userRepository,
        IActivityLogRepository activityLogRepository,
        IPasswordHasher passwordHasher,
        IHostingMode hostingMode,
        IPasswordResetTokenRepository resetTokenRepository,
        IEmailSender emailSender,
        ISettingsService settingsService)
    {
        _userRepository = userRepository;
        _activityLogRepository = activityLogRepository;
        _passwordHasher = passwordHasher;
        _hostingMode = hostingMode;
        _resetTokenRepository = resetTokenRepository;
        _emailSender = emailSender;
        _settingsService = settingsService;
    }

    // In professional-services hosted mode the first admin account is locked. Returns its id so it
    // can be protected from changes and presented as a SYSTEM account; null when not in that mode.
    private async Task<int?> GetLockedAdminIdAsync() =>
        _hostingMode.IsProfessionalServicesHosted
            ? (await _userRepository.GetAdminAsync())?.Id
            : null;

    private async Task GuardLockedAccountAsync(int id, string action)
    {
        if (await GetLockedAdminIdAsync() == id)
            throw new InvalidOperationException($"This is a protected system account and cannot be {action}.");
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
        return user is null ? null : MapToDto(user, await GetLockedAdminIdAsync());
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        var lockedAdminId = await GetLockedAdminIdAsync();
        return users.Select(u => MapToDto(u, lockedAdminId)).ToList();
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
        await GuardLockedAccountAsync(id, "modified");
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
        await GuardLockedAccountAsync(id, suspended ? "suspended" : "changed");
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
        await GuardLockedAccountAsync(id, "deleted");
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");


        await _userRepository.DeleteAsync(id);
    }

    public async Task RequestPasswordResetAsync(string usernameOrEmail, string requestHostBaseUrl)
    {
        usernameOrEmail = usernameOrEmail.Trim();
        var user = await _userRepository.GetByUsernameAsync(usernameOrEmail)
            ?? await _userRepository.GetByEmailAsync(usernameOrEmail);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
            return; // Enumeration-safe: silently no-op, same as a successful send from the caller's view.

        await _resetTokenRepository.InvalidateAllForUserAsync(user.Id);

        var rawToken = GenerateRawToken();
        await _resetTokenRepository.CreateAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(ResetTokenLifetime)
        });

        // Never trust the inbound request's Host header for a URL that carries a bearer
        // reset token -- it's attacker-controllable (Host header injection) and would let an
        // attacker redirect the emailed link to a domain they control. Use the admin-configured
        // public base URL when set; only fall back to the request's own host for installs that
        // haven't configured one yet (typically a local/LAN-only install with no exposed proxy).
        var configuredBaseUrl = await _settingsService.GetAsync(ISettingsService.PublicBaseUrlSettingKey);
        var resetBaseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl) ? requestHostBaseUrl : configuredBaseUrl;

        var link = $"{resetBaseUrl.TrimEnd('/')}/Auth/ResetPassword?token={Uri.EscapeDataString(rawToken)}";
        var html = $"""
            <div style="font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:520px;margin:auto;color:#111;">
              <h2 style="font-weight:600;">Reset your password</h2>
              <p>We received a request to reset the password for your Inventory Store account ({System.Net.WebUtility.HtmlEncode(user.Username)}). This link expires in 30 minutes.</p>
              <p style="margin:24px 0;"><a href="{System.Net.WebUtility.HtmlEncode(link)}"
                 style="background:#2563eb;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;">Reset password</a></p>
              <p>If the button doesn't work, paste this link into your browser:</p>
              <p style="word-break:break-all;"><a href="{System.Net.WebUtility.HtmlEncode(link)}">{System.Net.WebUtility.HtmlEncode(link)}</a></p>
              <p style="color:#666;">If you didn't request this, you can safely ignore this email -- your password won't change.</p>
            </div>
            """;

        var result = await _emailSender.SendAsync(user.Email, "Reset your password", html);
        if (result.Succeeded)
        {
            await _activityLogRepository.CreateAsync(new ActivityLog
            {
                UserId = user.Id,
                Username = user.Username,
                Action = "PasswordResetRequested",
                Details = "Password reset link emailed"
            });
        }
    }

    public async Task<bool> ResetPasswordWithTokenAsync(string token, string newPassword)
    {
        var resetToken = await _resetTokenRepository.GetValidByHashAsync(HashToken(token));
        if (resetToken is null)
            return false;

        var user = await _userRepository.GetByIdAsync(resetToken.UserId);
        if (user is null || !user.IsActive)
            return false;

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        await _userRepository.UpdateAsync(user);
        await _resetTokenRepository.MarkUsedAsync(resetToken.Id);

        await _activityLogRepository.CreateAsync(new ActivityLog
        {
            UserId = user.Id,
            Username = user.Username,
            Action = "PasswordResetCompleted",
            Details = "Password reset via emailed link"
        });

        return true;
    }

    private static string GenerateRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static UserDto MapToDto(User user, int? lockedAdminId = null) => new(
        user.Id,
        user.Username,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role,
        user.IsActive,
        user.CreatedAt,
        user.LastLoginAt,
        IsSystemLocked: lockedAdminId == user.Id
    );
}
