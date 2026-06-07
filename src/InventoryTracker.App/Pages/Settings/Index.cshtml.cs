using InventoryTracker.App.Extensions;
using InventoryTracker.App.Services;
using InventoryTracker.App.Utilities;
using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using InventoryTracker.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages.Settings;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUserAuthService _authService;
    private readonly ISettingsService _settingsService;
    private readonly TunnelService _tunnel;

    public string Tab { get; private set; } = "account";
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IEnumerable<UserDto> Users { get; private set; } = [];
    public string LocalIpAddress { get; private set; } = "localhost";

    // Tunnel state (read from singleton, used for initial render)
    public TunnelService.TunnelState TunnelState => _tunnel.State;
    public string? TunnelPublicUrl => _tunnel.PublicUrl;
    public string? TunnelError => _tunnel.Error;

    public string? SavedTunnelToken  { get; private set; }
    public string? SavedTunnelUrl    { get; private set; }
    public string? SavedLtSubdomain  { get; private set; }
    public string? SavedAutostart    { get; private set; }

    public IndexModel(IUserAuthService authService, ISettingsService settingsService, TunnelService tunnel)
    {
        _authService     = authService;
        _settingsService = settingsService;
        _tunnel          = tunnel;
    }

    public async Task OnGetAsync(string tab = "account", string? success = null, string? error = null)
    {
        Tab            = tab;
        SuccessMessage = success;
        ErrorMessage   = error;
        LocalIpAddress = NetworkUtility.GetLocalIpAddress();

        if (tab == "users")
            Users = await _authService.GetAllUsersAsync();

        if (tab == "tunnel")
        {
            SavedTunnelToken = await _settingsService.GetAsync("tunnel.token");
            SavedTunnelUrl   = await _settingsService.GetAsync("tunnel.url");
            SavedLtSubdomain = await _settingsService.GetAsync("tunnel.lt.subdomain");
            SavedAutostart   = await _settingsService.GetAsync("tunnel.autostart");
        }
    }

    public async Task<IActionResult> OnPostSaveTunnelConfigAsync(
        string? tunnelToken, string? tunnelUrl, string? ltSubdomain, string? autostart)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await _settingsService.SetAsync("tunnel.token",         tunnelToken);
        await _settingsService.SetAsync("tunnel.url",           tunnelUrl);
        await _settingsService.SetAsync("tunnel.lt.subdomain",  ltSubdomain);
        await _settingsService.SetAsync("tunnel.autostart",     autostart);

        return RedirectWithMessage("tunnel", success: "Tunnel settings saved.");
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        var (userId, username) = User.GetIdentity();
        var valid = await _authService.ValidateCredentialsAsync(username, currentPassword);
        if (valid is null)
            return RedirectWithMessage("account", error: "Current password is incorrect.");

        if (newPassword.Length < 8)
            return RedirectWithMessage("account", error: "Password must be at least 8 characters.");

        if (newPassword != confirmPassword)
            return RedirectWithMessage("account", error: "Passwords do not match.");

        await _authService.ResetUserPasswordAsync(userId, newPassword);
        return RedirectWithMessage("account", success: "Password updated successfully.");
    }

    public async Task<IActionResult> OnPostAddUserAsync(AddUserInput add)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        try
        {
            await _authService.CreateUserAsync(new CreateUserDto(
                add.Username, add.FirstName, add.LastName, add.Email, add.Password, add.Role));
            return RedirectWithMessage("users", success: $"User '{add.Username}' created.");
        }
        catch (Exception ex) { return RedirectWithMessage("users", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostEditUserAsync(EditUserInput edit)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        try
        {
            await _authService.UpdateUserAsync(edit.UserId,
                new UpdateUserDto(edit.FirstName, edit.LastName, edit.Email, edit.Role, edit.IsActive));

            if (!string.IsNullOrWhiteSpace(edit.NewPassword))
            {
                if (edit.NewPassword.Length < 8)
                    return RedirectWithMessage("users", error: "New password must be at least 8 characters.");
                await _authService.ResetUserPasswordAsync(edit.UserId, edit.NewPassword);
            }
            return RedirectWithMessage("users", success: "User updated.");
        }
        catch (Exception ex) { return RedirectWithMessage("users", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostToggleSuspendAsync(int userId, bool suspend)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        try
        {
            await _authService.SetUserSuspendedAsync(userId, suspend);
            return RedirectWithMessage("users", success: suspend ? "User suspended." : "User reactivated.");
        }
        catch (Exception ex) { return RedirectWithMessage("users", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(int userId)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        try
        {
            await _authService.DeleteUserAsync(userId);
            return RedirectWithMessage("users", success: "User deleted.");
        }
        catch (Exception ex) { return RedirectWithMessage("users", error: ex.Message); }
    }

    private IActionResult RedirectWithMessage(string tab, string? success = null, string? error = null) =>
        RedirectToPage(new { tab, success, error });

    public class AddUserInput
    {
        public string? FirstName { get; set; }
        public string? LastName  { get; set; }
        public string Username   { get; set; } = string.Empty;
        public string? Email     { get; set; }
        public string Password   { get; set; } = string.Empty;
        public UserRole Role     { get; set; } = UserRole.Viewer;
    }

    public class EditUserInput
    {
        public int UserId        { get; set; }
        public string? FirstName { get; set; }
        public string? LastName  { get; set; }
        public string? Email     { get; set; }
        public UserRole Role     { get; set; } = UserRole.Viewer;
        public string? NewPassword { get; set; }
        public bool IsActive     { get; set; } = true;
    }
}
