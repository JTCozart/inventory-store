using InventoryTracker.App.Extensions;
using InventoryTracker.App.Services;
using InventoryTracker.App.Utilities;
using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using InventoryTracker.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace InventoryTracker.App.Pages.Settings;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUserAuthService _authService;
    private readonly ISettingsService _settingsService;
    private readonly TunnelService _tunnel;
    private readonly ICategoryService _categoryService;
    private readonly IInventoryService _inventoryService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<IndexModel> _logger;

    public string Tab { get; private set; } = "account";
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IEnumerable<UserDto> Users { get; private set; } = [];
    public IEnumerable<CategoryDto> Categories { get; private set; } = [];
    public IEnumerable<LocationSummaryDto> Locations { get; private set; } = [];
    public string LocalIpAddress { get; private set; } = "localhost";

    // Tunnel state (read from singleton, used for initial render)
    public TunnelService.TunnelState TunnelState => _tunnel.State;
    public string? TunnelPublicUrl => _tunnel.PublicUrl;
    public string? TunnelError => _tunnel.Error;

    public string? SavedTunnelToken      { get; private set; }
    public string? SavedTunnelUrl        { get; private set; }
    public string? SavedLtSubdomain      { get; private set; }
    public string? SavedAutostart        { get; private set; }
    public string? SavedServeoSubdomain  { get; private set; }
    public string? ServeoPublicKey       { get; private set; }

    // Public view tab
    public bool PublicViewEnabled          { get; private set; }
    public string PublicViewUrl            { get; private set; } = string.Empty;
    public IEnumerable<InventoryItemDto> AllItems { get; private set; } = [];

    // Current user id — used to prevent self-modification in user management
    public int CurrentUserId               { get; private set; }

    public IndexModel(IUserAuthService authService, ISettingsService settingsService, TunnelService tunnel,
        ICategoryService categoryService, IInventoryService inventoryService,
        IHttpContextAccessor httpContextAccessor, ILogger<IndexModel> logger)
    {
        _authService          = authService;
        _settingsService      = settingsService;
        _tunnel               = tunnel;
        _categoryService      = categoryService;
        _inventoryService     = inventoryService;
        _httpContextAccessor  = httpContextAccessor;
        _logger               = logger;
    }

    public async Task OnGetAsync(string tab = "account", string? success = null, string? error = null)
    {
        Tab            = tab;
        SuccessMessage = success;
        ErrorMessage   = error;
        LocalIpAddress = NetworkUtility.GetLocalIpAddress();
        CurrentUserId  = User.GetIdentity().userId;

        if (tab == "users")
            Users = await _authService.GetAllUsersAsync();

        if (tab == "categories")
            Categories = await _categoryService.GetAllAsync();

        if (tab == "locations")
            Locations = await _inventoryService.GetAllLocationsAsync();

        if (tab == "tunnel")
        {
            SavedTunnelToken     = await _settingsService.GetAsync("tunnel.token");
            SavedTunnelUrl       = await _settingsService.GetAsync("tunnel.url");
            SavedLtSubdomain     = await _settingsService.GetAsync("tunnel.lt.subdomain");
            SavedAutostart       = await _settingsService.GetAsync("tunnel.autostart");
            SavedServeoSubdomain = await _settingsService.GetAsync("tunnel.serveo.subdomain");
            ServeoPublicKey      = await _tunnel.GetServeoPublicKeyAsync();
        }

        if (tab == "publicview")
        {
            PublicViewEnabled = await _settingsService.GetAsync("public.view.enabled") == "true";
            AllItems          = await _inventoryService.GetAllItemsAsync();
            var req           = _httpContextAccessor.HttpContext?.Request;
            PublicViewUrl     = req is not null
                ? $"{req.Scheme}://{req.Host}/public"
                : "/public";
        }
    }

    public async Task<IActionResult> OnPostSaveTunnelConfigAsync(
        string? tunnelToken, string? tunnelUrl, string? ltSubdomain, string? autostart, string? serveoSubdomain)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await _settingsService.SetAsync("tunnel.token",              tunnelToken);
        await _settingsService.SetAsync("tunnel.url",                tunnelUrl);
        await _settingsService.SetAsync("tunnel.lt.subdomain",       ltSubdomain);
        await _settingsService.SetAsync("tunnel.autostart",          autostart);
        await _settingsService.SetAsync("tunnel.serveo.subdomain",   serveoSubdomain);

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
        if (userId == User.GetIdentity().userId)
            return RedirectWithMessage("users", error: "You cannot suspend your own account.");
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
        if (userId == User.GetIdentity().userId)
            return RedirectWithMessage("users", error: "You cannot delete your own account.");
        try
        {
            await _authService.DeleteUserAsync(userId);
            return RedirectWithMessage("users", success: "User deleted.");
        }
        catch (Exception ex) { return RedirectWithMessage("users", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostRenameLocationAsync(string from, string to)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return RedirectWithMessage("locations", error: "Both current and new location names are required.");
        try
        {
            var (uid, uname) = User.GetIdentity();
            await _inventoryService.RenameLocationAsync(from, to, uid, uname);
            return RedirectWithMessage("locations", success: $"Location '{from}' renamed to '{to}'.");
        }
        catch (Exception ex) { return RedirectWithMessage("locations", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostClearLocationAsync(string location)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        try
        {
            var (uid, uname) = User.GetIdentity();
            await _inventoryService.ClearLocationAsync(location, uid, uname);
            return RedirectWithMessage("locations", success: $"Location '{location}' removed from all items.");
        }
        catch (Exception ex) { return RedirectWithMessage("locations", error: ex.Message); }
    }

    public async Task<IActionResult> OnGetCategoryItemsJsonAsync(int categoryId)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        var all = await _inventoryService.GetAllItemsAsync();
        var inCategory = all
            .Where(i => i.CategoryId == categoryId)
            .Select(i => new { i.Id, i.Name, i.SKU })
            .ToList();
        var others = all
            .Where(i => i.CategoryId != categoryId)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.SKU, Category = i.CategoryName ?? "" })
            .ToList();
        return new JsonResult(new { inCategory, others }, InventoryTracker.App.Infrastructure.AppJsonOptions.Web);
    }

    public async Task<IActionResult> OnPostSetItemCategoryAsync(int itemId, int? newCategoryId)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        try
        {
            var (uid, uname) = User.GetIdentity();
            await _inventoryService.SetItemCategoryAsync(itemId, newCategoryId == 0 ? null : newCategoryId, uid, uname);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnGetLocationItemsJsonAsync(string location)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        var all = await _inventoryService.GetAllItemsAsync();
        var atLocation = all
            .Where(i => i.Location == location)
            .Select(i => new { i.Id, i.Name, i.SKU })
            .ToList();
        var others = all
            .Where(i => i.Location != location)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.SKU, Location = i.Location ?? "" })
            .ToList();
        return new JsonResult(new { atLocation, others }, InventoryTracker.App.Infrastructure.AppJsonOptions.Web);
    }

    public async Task<IActionResult> OnPostSetItemLocationAsync(int itemId, string? newLocation)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        try
        {
            var (uid, uname) = User.GetIdentity();
            await _inventoryService.SetItemLocationAsync(itemId, newLocation, uid, uname);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> OnGetLocationsJsonAsync()
    {
        var locations = await _inventoryService.GetAllLocationsAsync();
        return new JsonResult(locations.Select(l => l.Name));
    }

    public async Task<IActionResult> OnPostAddCategoryAsync(string name, string? color)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        try
        {
            await _categoryService.CreateAsync(new Application.DTOs.CreateCategoryDto(name, color));
            return RedirectWithMessage("categories", success: $"Category '{name}' created.");
        }
        catch (Exception ex) { return RedirectWithMessage("categories", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostEditCategoryAsync(int categoryId, string name, string? color)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        try
        {
            await _categoryService.UpdateAsync(categoryId, new Application.DTOs.UpdateCategoryDto(name, color));
            return RedirectWithMessage("categories", success: "Category updated.");
        }
        catch (Exception ex) { return RedirectWithMessage("categories", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostDeleteCategoryAsync(int categoryId)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        try
        {
            await _categoryService.DeleteAsync(categoryId);
            return RedirectWithMessage("categories", success: "Category deleted.");
        }
        catch (Exception ex) { return RedirectWithMessage("categories", error: ex.Message); }
    }

    public async Task<IActionResult> OnGetDownloadBackupAsync()
    {
        if (!User.IsInRole("Admin")) return Forbid();

        var dbPath = GetDbPath();
        if (!System.IO.File.Exists(dbPath))
            return RedirectWithMessage("database", error: "Database file not found.");

        // Use a path with no SQL-special characters so we can safely embed it in VACUUM INTO.
        var tempPath = Path.Combine(Path.GetTempPath(), $"invt_{Guid.NewGuid():N}.db");
        try
        {
            try
            {
                // VACUUM INTO creates a clean, WAL-free snapshot in one operation.
                // Unlike BackupDatabase(), it does not leave journal files behind,
                // so the temp file is fully released when this connection disposes.
                using var conn = new SqliteConnection($"Data Source={dbPath}");
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"VACUUM INTO '{tempPath.Replace("'", "''")}'";
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup failed.");
                return RedirectWithMessage("database", error: "Backup failed: " + ex.Message);
            }

            var bytes    = await System.IO.File.ReadAllBytesAsync(tempPath);
            var filename = $"inventory_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            return File(bytes, "application/octet-stream", filename);
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { /* temp file, best-effort */ }
        }
    }

    public async Task<IActionResult> OnPostRestoreBackupAsync(IFormFile? backupFile)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (backupFile is null || backupFile.Length == 0)
            return RedirectWithMessage("database", error: "No file selected.");

        // Validate SQLite magic bytes
        var header = new byte[16];
        await using (var peek = backupFile.OpenReadStream())
            _ = await peek.ReadAsync(header.AsMemory(0, 16));

        if (System.Text.Encoding.ASCII.GetString(header, 0, 15) != "SQLite format 3")
            return RedirectWithMessage("database", error: "Invalid file — please upload a valid SQLite database backup (.db).");

        var dbPath  = GetDbPath();
        var bakPath = dbPath + ".bak";
        var tmpPath = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid():N}.db");

        try
        {
            await using (var src  = backupFile.OpenReadStream())
            await using (var dest = System.IO.File.OpenWrite(tmpPath))
                await src.CopyToAsync(dest);

            // Release all pooled SQLite connections so the file can be replaced
            SqliteConnection.ClearAllPools();

            if (System.IO.File.Exists(dbPath))
                System.IO.File.Copy(dbPath, bakPath, overwrite: true);

            System.IO.File.Copy(tmpPath, dbPath, overwrite: true);

            // Remove stale WAL/SHM files from the old database
            foreach (var ext in new[] { "-wal", "-shm" })
            {
                var f = dbPath + ext;
                if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database restore failed.");
            return RedirectWithMessage("database", error: "Restore failed: " + ex.Message);
        }
        finally
        {
            if (System.IO.File.Exists(tmpPath)) System.IO.File.Delete(tmpPath);
        }

        return RedirectWithMessage("database", success: "Database restored successfully. A backup of the previous database was saved alongside it.");
    }

    private static string GetDbPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InventoryTracker", "inventory.db");

    public async Task<IActionResult> OnPostSavePublicViewSettingAsync(bool enabled)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        await _settingsService.SetAsync("public.view.enabled", enabled ? "true" : null);
        return RedirectWithMessage("publicview", success: enabled ? "Public view enabled." : "Public view disabled.");
    }

    public async Task<IActionResult> OnPostTogglePublicItemAsync(int itemId, bool isPublic)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        await _inventoryService.SetItemPublicAsync(itemId, isPublic);
        return new JsonResult(new { success = true });
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
