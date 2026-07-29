using InventoryStore.App.Extensions;
using InventoryStore.App.Services;
using InventoryStore.App.Utilities;
using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using InventoryStore.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using System.Security.Claims;

namespace InventoryStore.App.Pages.Settings;

// Staff are scoped to the Terminal and have no settings access.
[Authorize(Roles = "Admin,Manager,Viewer")]
public class IndexModel : PageModel
{
    private readonly IUserAuthService _authService;
    private readonly ISettingsService _settingsService;
    private readonly AppTimeZone _appTimeZone;
    private readonly TunnelService _tunnel;
    private readonly ICategoryService _categoryService;
    private readonly ITagService _tagService;
    private readonly IInventoryService _inventoryService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly InventoryStore.App.Modules.IModuleRegistry _modules;
    private readonly InventoryStore.App.Email.EmailSender _emailSender;
    private readonly InventoryStore.Application.Interfaces.Services.IHostingMode _hostingMode;
    private readonly InventoryStore.Domain.Interfaces.Repositories.IUserRepository _userRepository;
    private readonly ILogger<IndexModel> _logger;

    public string Tab { get; private set; } = "account";
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IEnumerable<UserDto> Users { get; private set; } = [];
    public IEnumerable<CategoryDto> Categories { get; private set; } = [];
    public IEnumerable<TagDto> Tags { get; private set; } = [];
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

    // General tab — the instance-wide display time zone (the account's physical location).
    // Empty means "no zone set" — the browser uses each viewer's own device zone.
    public string TimeZoneId { get; private set; } = string.Empty;
    public IReadOnlyList<InventoryStore.App.Utilities.TimeZones.TzOption> AvailableTimeZones
        => InventoryStore.App.Utilities.TimeZones.Options;

    // HTTPS tab
    public bool    HttpsEnabled       { get; private set; }
    public int     HttpsPort          { get; private set; } = 443;
    public string? HttpsDomain        { get; private set; }
    public bool    HttpsCertUploaded  { get; private set; }
    public string  HttpsMode          { get; private set; } = "manual";
    public string? LeEmail            { get; private set; }
    public bool    LeTosAccepted      { get; private set; }

    // Modules tab
    public IReadOnlyList<InventoryStore.App.Modules.ModuleDescriptor> Modules { get; private set; } = [];
    public IReadOnlyDictionary<string, bool> EnabledMap { get; private set; } = new Dictionary<string, bool>();
    public string? ModuleSection { get; private set; }
    public bool IsModuleEnabled(string key) => EnabledMap.TryGetValue(key, out var v) && v;
    // Loaded on every tab so the sidenav can show the Vendors link only when the module is on.
    public bool MaintenanceModuleEnabled { get; private set; }
    // Per-module Configure state
    public string CostCurrency      { get; private set; } = "$";
    public int    ForecastWindowDays { get; private set; } = 30;
    public bool   KitsReconcileEnabled { get; private set; }
    public string KitsReconcileMode    { get; private set; } = "used";
    public bool   AiApiKeySet { get; private set; }
    public string? AiModel    { get; private set; }
    public bool    EmailApiKeySet   { get; private set; }
    public bool    EmailSecretKeySet { get; private set; }
    public string? EmailFromAddress { get; private set; }
    public string? EmailFromName    { get; private set; }

    // General tab — public base URL, used wherever the app needs to build an absolute,
    // externally-reachable URL server-side (currently: password-reset links).
    public string? PublicBaseUrl    { get; private set; }

    // Hosted-mode gating: when the server runs PROFESSIONAL_SERVICES_HOSTED=true, only the
    // primary (first-created) admin may view/edit settings that are sensitive enough to matter
    // for a hosted customer's trust boundary -- currently the Mailjet credentials and the public
    // base URL (which a malicious admin could otherwise point at a domain they control to steal
    // other users' password-reset tokens). Any admin may manage these in self-hosted mode.
    public bool    CanManageProtectedSettings { get; private set; }

    // Notifications tab
    public string? NtfyServer      { get; private set; }
    public string? NtfyTopic       { get; private set; }
    public bool    NtfyTokenSet    { get; private set; }
    public bool    NtfyOnCheckout  { get; private set; }
    public bool    NtfyOnCheckin   { get; private set; }
    public bool    NtfyOnLost      { get; private set; }
    public bool    NtfyOnLowStock  { get; private set; }
    public bool    NtfyOnLogin     { get; private set; }

    public IndexModel(IUserAuthService authService, ISettingsService settingsService, AppTimeZone appTimeZone,
        TunnelService tunnel,
        ICategoryService categoryService, ITagService tagService, IInventoryService inventoryService,
        IHttpContextAccessor httpContextAccessor, InventoryStore.App.Modules.IModuleRegistry modules,
        InventoryStore.App.Email.EmailSender emailSender,
        InventoryStore.Application.Interfaces.Services.IHostingMode hostingMode,
        InventoryStore.Domain.Interfaces.Repositories.IUserRepository userRepository,
        ILogger<IndexModel> logger)
    {
        _authService          = authService;
        _settingsService      = settingsService;
        _appTimeZone          = appTimeZone;
        _tunnel               = tunnel;
        _categoryService      = categoryService;
        _tagService           = tagService;
        _inventoryService     = inventoryService;
        _httpContextAccessor  = httpContextAccessor;
        _modules              = modules;
        _emailSender          = emailSender;
        _hostingMode          = hostingMode;
        _userRepository       = userRepository;
        _logger               = logger;
    }

    // Same "locked admin" logic AuthenticationService uses to protect the primary account:
    // in hosted mode only that account may manage protected settings; any admin may otherwise.
    private async Task<bool> ComputeCanManageProtectedSettingsAsync()
    {
        if (!_hostingMode.IsProfessionalServicesHosted)
            return true;
        var admin = await _userRepository.GetAdminAsync();
        return admin?.Id == CurrentUserId;
    }

    public async Task OnGetAsync(string tab = "account", string? success = null, string? error = null, string? section = null)
    {
        Tab            = tab;
        SuccessMessage = success;
        ErrorMessage   = error;
        LocalIpAddress = NetworkUtility.GetLocalIpAddress();
        CurrentUserId  = User.GetIdentity().userId;
        MaintenanceModuleEnabled = await _modules.IsEnabledAsync("maintenance");
        CanManageProtectedSettings = await ComputeCanManageProtectedSettingsAsync();

        if (tab == "general")
        {
            // Empty means "no override" — fall back to the server's local zone.
            TimeZoneId = await _settingsService.GetAsync(AppTimeZone.SettingKey) ?? string.Empty;
            PublicBaseUrl = await _settingsService.GetAsync(ISettingsService.PublicBaseUrlSettingKey);
        }

        if (tab == "users")
            Users = await _authService.GetAllUsersAsync();

        if (tab == "categories")
            Categories = await _categoryService.GetAllAsync();

        if (tab == "tags")
            Tags = await _tagService.GetAllAsync();

        if (tab == "locations")
            Locations = await _inventoryService.GetAllLocationsAsync();

        if (tab == "network")
        {
            SavedTunnelToken     = await _settingsService.GetAsync("tunnel.token");
            SavedTunnelUrl       = await _settingsService.GetAsync("tunnel.url");
            SavedLtSubdomain     = await _settingsService.GetAsync("tunnel.lt.subdomain");
            SavedAutostart       = await _settingsService.GetAsync("tunnel.autostart");
            SavedServeoSubdomain = await _settingsService.GetAsync("tunnel.serveo.subdomain");
            ServeoPublicKey      = await _tunnel.GetServeoPublicKeyAsync();

            HttpsEnabled  = await _settingsService.GetAsync("https.enabled") == "true";
            HttpsDomain   = await _settingsService.GetAsync("https.domain");
            var portStr   = await _settingsService.GetAsync("https.port");
            HttpsPort     = int.TryParse(portStr, out var p) ? p : 443;
            HttpsCertUploaded = System.IO.File.Exists(HttpsCertPath());
            HttpsMode     = await _settingsService.GetAsync("https.mode") ?? "manual";
            LeEmail       = await _settingsService.GetAsync("https.letsencrypt.email");
            LeTosAccepted = await _settingsService.GetAsync("https.letsencrypt.tos") == "true";
        }

        if (tab == "modules")
        {
            ModuleSection      = section;
            Modules            = _modules.All;
            EnabledMap         = await _modules.GetEnabledMapAsync();
            CostCurrency       = await _settingsService.GetAsync("module.cost.currency") ?? "$";
            ForecastWindowDays = int.TryParse(await _settingsService.GetAsync("module.forecast.windowdays"), out var w) && w > 0 ? w : 30;
            KitsReconcileEnabled = await _settingsService.GetAsync("module.kits.reconcile") == "true";
            KitsReconcileMode    = await _settingsService.GetAsync("module.kits.reconcile.mode") ?? "used";
            AiApiKeySet = !string.IsNullOrWhiteSpace(await _settingsService.GetAsync("module.ai.apiKey"));
            AiModel     = await _settingsService.GetAsync("module.ai.model");

            EmailApiKeySet    = !string.IsNullOrWhiteSpace(await _settingsService.GetAsync(InventoryStore.App.Email.EmailSender.ApiKeySettingKey));
            EmailSecretKeySet = !string.IsNullOrWhiteSpace(await _settingsService.GetAsync(InventoryStore.App.Email.EmailSender.SecretKeySettingKey));
            EmailFromAddress  = await _settingsService.GetAsync(InventoryStore.App.Email.EmailSender.FromAddressSettingKey);
            EmailFromName     = await _settingsService.GetAsync(InventoryStore.App.Email.EmailSender.FromNameSettingKey);
        }

        if (tab == "notifications")
        {
            NtfyServer     = await _settingsService.GetAsync("ntfy.server") ?? "https://ntfy.sh";
            NtfyTopic      = await _settingsService.GetAsync("ntfy.topic");
            NtfyTokenSet   = !string.IsNullOrWhiteSpace(await _settingsService.GetAsync("ntfy.token"));
            NtfyOnCheckout = await _settingsService.GetAsync("ntfy.notify.checkout") == "true";
            NtfyOnCheckin  = await _settingsService.GetAsync("ntfy.notify.checkin")  == "true";
            NtfyOnLost     = await _settingsService.GetAsync("ntfy.notify.lost")     == "true";
            NtfyOnLowStock = await _settingsService.GetAsync("ntfy.notify.lowstock") == "true";
            NtfyOnLogin    = await _settingsService.GetAsync("ntfy.notify.login")    == "true";
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

    public async Task<IActionResult> OnPostSaveGeneralAsync(string? timeZoneId)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        timeZoneId = timeZoneId?.Trim();
        // Empty clears the setting — the browser then uses each viewer's own device zone.
        if (!string.IsNullOrEmpty(timeZoneId) && !InventoryStore.App.Utilities.TimeZones.IsValid(timeZoneId))
            return RedirectWithMessage("general", error: "Please choose a valid time zone.");

        await _settingsService.SetAsync(AppTimeZone.SettingKey, string.IsNullOrEmpty(timeZoneId) ? null : timeZoneId);
        _appTimeZone.Set(timeZoneId);

        return RedirectWithMessage("general", success: string.IsNullOrEmpty(timeZoneId)
            ? "Time zone cleared. Times now follow each viewer's device."
            : "Time zone saved. Times now display in the selected zone.");
    }

    public async Task<IActionResult> OnPostSavePublicBaseUrlAsync(string? publicBaseUrl)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        CurrentUserId = User.GetIdentity().userId;
        if (!await ComputeCanManageProtectedSettingsAsync()) return Forbid();

        publicBaseUrl = publicBaseUrl?.Trim();
        if (!string.IsNullOrEmpty(publicBaseUrl)
            && !(Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var parsed)
                 && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)))
        {
            return RedirectWithMessage("general", error: "Public base URL must be a valid http(s) URL, e.g. https://inventory.example.com.");
        }

        await _settingsService.SetAsync(ISettingsService.PublicBaseUrlSettingKey,
            string.IsNullOrEmpty(publicBaseUrl) ? null : publicBaseUrl.TrimEnd('/'));

        return RedirectWithMessage("general", success: "Public base URL saved.");
    }

    public async Task<IActionResult> OnPostSaveNotificationsAsync(
        string? ntfyServer, string? ntfyTopic, string? ntfyToken,
        bool ntfyOnCheckout, bool ntfyOnCheckin, bool ntfyOnLost, bool ntfyOnLowStock, bool ntfyOnLogin)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await _settingsService.SetAsync("ntfy.server",          string.IsNullOrWhiteSpace(ntfyServer) ? "https://ntfy.sh" : ntfyServer.Trim());
        await _settingsService.SetAsync("ntfy.topic",           ntfyTopic?.Trim());
        if (!string.IsNullOrWhiteSpace(ntfyToken))
            await _settingsService.SetAsync("ntfy.token",       ntfyToken.Trim());
        await _settingsService.SetAsync("ntfy.notify.checkout", ntfyOnCheckout ? "true" : "false");
        await _settingsService.SetAsync("ntfy.notify.checkin",  ntfyOnCheckin  ? "true" : "false");
        await _settingsService.SetAsync("ntfy.notify.lost",     ntfyOnLost     ? "true" : "false");
        await _settingsService.SetAsync("ntfy.notify.lowstock", ntfyOnLowStock ? "true" : "false");
        await _settingsService.SetAsync("ntfy.notify.login",    ntfyOnLogin    ? "true" : "false");

        return RedirectWithMessage("notifications", success: "Notification settings saved.");
    }

    public async Task<IActionResult> OnPostClearNtfyTokenAsync()
    {
        if (!User.IsInRole("Admin")) return Forbid();
        await _settingsService.SetAsync("ntfy.token", null);
        return RedirectWithMessage("notifications", success: "Token cleared.");
    }

    public async Task<IActionResult> OnPostSaveHttpsAsync(
        bool httpsEnabled, int httpsPort, string? httpsDomain, string? httpsCertPassword, IFormFile? certFile)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        if (certFile is { Length: > 0 })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HttpsCertPath())!);
            await using var stream = System.IO.File.Create(HttpsCertPath());
            await certFile.CopyToAsync(stream);
        }

        await _settingsService.SetAsync("https.mode",    "manual");
        await _settingsService.SetAsync("https.enabled", httpsEnabled ? "true" : "false");
        await _settingsService.SetAsync("https.port",    httpsPort > 0 ? httpsPort.ToString() : "443");
        await _settingsService.SetAsync("https.domain",  httpsDomain?.Trim());
        if (!string.IsNullOrWhiteSpace(httpsCertPassword))
            await _settingsService.SetAsync("https.cert.password", httpsCertPassword);

        return RedirectWithMessage("network", success: "HTTPS settings saved. Restart the service for changes to take effect.");
    }

    public async Task<IActionResult> OnPostSaveLetsEncryptAsync(
        bool leEnabled, string? leDomain, string? leEmail, bool leAcceptTos)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        leDomain = leDomain?.Trim();
        leEmail  = leEmail?.Trim();

        if (leEnabled)
        {
            if (string.IsNullOrWhiteSpace(leDomain))
                return RedirectWithMessage("network", error: "A domain name is required for Let's Encrypt.");
            if (string.IsNullOrWhiteSpace(leEmail))
                return RedirectWithMessage("network", error: "An email address is required for Let's Encrypt.");
            if (!leAcceptTos)
                return RedirectWithMessage("network", error: "You must accept the Let's Encrypt Terms of Service.");
        }

        await _settingsService.SetAsync("https.mode",              "letsencrypt");
        await _settingsService.SetAsync("https.enabled",           leEnabled ? "true" : "false");
        await _settingsService.SetAsync("https.port",              "443");
        await _settingsService.SetAsync("https.domain",            leDomain);
        await _settingsService.SetAsync("https.letsencrypt.email", leEmail);
        await _settingsService.SetAsync("https.letsencrypt.tos",   leAcceptTos ? "true" : "false");

        return RedirectWithMessage("network", success:
            "Let's Encrypt settings saved. Restart the service to obtain and enable the certificate. " +
            "Make sure your domain points to this server and ports 80 and 443 are reachable.");
    }

    public async Task<IActionResult> OnPostClearHttpsCertAsync()
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (System.IO.File.Exists(HttpsCertPath())) System.IO.File.Delete(HttpsCertPath());
        await _settingsService.SetAsync("https.enabled",       "false");
        await _settingsService.SetAsync("https.cert.password", null);
        return RedirectWithMessage("network", success: "Certificate removed. Restart the service to disable HTTPS.");
    }

    private static string HttpsCertPath() => Path.Combine(AppPaths.DataDir, "https.pfx");

    public async Task<IActionResult> OnPostSaveTunnelConfigAsync(
        string? tunnelToken, string? tunnelUrl, string? ltSubdomain, string? autostart, string? serveoSubdomain)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        await _settingsService.SetAsync("tunnel.token",              tunnelToken);
        await _settingsService.SetAsync("tunnel.url",                tunnelUrl);
        await _settingsService.SetAsync("tunnel.lt.subdomain",       ltSubdomain);
        await _settingsService.SetAsync("tunnel.autostart",          autostart);
        await _settingsService.SetAsync("tunnel.serveo.subdomain",   serveoSubdomain);

        return RedirectWithMessage("network", success: "Tunnel settings saved.");
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
        return new JsonResult(new { inCategory, others }, InventoryStore.App.Infrastructure.AppJsonOptions.Web);
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
        return new JsonResult(new { atLocation, others }, InventoryStore.App.Infrastructure.AppJsonOptions.Web);
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

    public async Task<IActionResult> OnPostAddTagAsync(string name)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        try
        {
            await _tagService.CreateAsync(new Application.DTOs.CreateTagDto(name));
            return RedirectWithMessage("tags", success: $"Tag '{name}' created.");
        }
        catch (Exception ex) { return RedirectWithMessage("tags", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostEditTagAsync(int tagId, string name)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        try
        {
            await _tagService.UpdateAsync(tagId, new Application.DTOs.UpdateTagDto(name));
            return RedirectWithMessage("tags", success: "Tag updated.");
        }
        catch (Exception ex) { return RedirectWithMessage("tags", error: ex.Message); }
    }

    public async Task<IActionResult> OnPostDeleteTagAsync(int tagId)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        try
        {
            await _tagService.DeleteAsync(tagId);
            return RedirectWithMessage("tags", success: "Tag deleted.");
        }
        catch (Exception ex) { return RedirectWithMessage("tags", error: ex.Message); }
    }

    public async Task<IActionResult> OnGetTagItemsJsonAsync(int tagId)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager")) return Forbid();
        var all = await _inventoryService.GetAllItemsAsync();
        var tagged = all
            .Where(i => i.Tags.Any(t => t.Id == tagId))
            .Select(i => new { i.Id, i.Name, i.SKU })
            .ToList();
        var others = all
            .Where(i => i.Tags.All(t => t.Id != tagId))
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.SKU })
            .ToList();
        return new JsonResult(new { tagged, others }, InventoryStore.App.Infrastructure.AppJsonOptions.Web);
    }

    public async Task<IActionResult> OnPostSetItemTagAsync(int itemId, int tagId, bool assigned)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            return new JsonResult(new { success = false, error = "Insufficient permissions." }) { StatusCode = 403 };
        try
        {
            var (uid, uname) = User.GetIdentity();
            await _inventoryService.SetItemTagAsync(itemId, tagId, assigned, uid, uname);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
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
            var filename = $"inventory_backup_{_appTimeZone.Now():yyyyMMdd_HHmmss}.db";
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

    private static string GetDbPath() => Path.Combine(AppPaths.DataDir, "inventory.db");

    public async Task<IActionResult> OnPostToggleModuleAsync(string key, bool enabled)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        var module = _modules.Find(key);
        if (module is null) return RedirectWithMessage("modules", error: "Unknown module.");
        if (string.Equals(key, "email", StringComparison.OrdinalIgnoreCase))
        {
            CurrentUserId = User.GetIdentity().userId;
            if (!await ComputeCanManageProtectedSettingsAsync()) return Forbid();
        }
        await _modules.SetEnabledAsync(key, enabled);
        return RedirectWithMessage("modules",
            success: $"{module.Name} module {(enabled ? "enabled" : "disabled")}.");
    }

    public async Task<IActionResult> OnPostSaveCostSettingsAsync(string? currency)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        await _settingsService.SetAsync("module.cost.currency",
            string.IsNullOrWhiteSpace(currency) ? "$" : currency.Trim());
        return RedirectWithMessage("modules", success: "Currency saved.", section: "cost");
    }

    public async Task<IActionResult> OnPostSaveForecastSettingsAsync(int windowDays)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        await _settingsService.SetAsync("module.forecast.windowdays", (windowDays > 0 ? windowDays : 30).ToString());
        return RedirectWithMessage("modules", success: "Forecast window saved.", section: "forecast");
    }

    public async Task<IActionResult> OnPostSaveKitsSettingsAsync(bool reconcileEnabled, string? mode)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        await _settingsService.SetAsync("module.kits.reconcile", reconcileEnabled ? "true" : null);
        await _settingsService.SetAsync("module.kits.reconcile.mode", mode == "remain" ? "remain" : "used");
        return RedirectWithMessage("modules", success: "Kit settings saved.", section: "kits");
    }

    // Blank apiKey means "keep the existing key" (mirrors the not-shown password input) --
    // there is no way to clear it back to unconfigured from this form, only replace it.
    public async Task<IActionResult> OnPostSaveAiSettingsAsync(string? apiKey, string? model)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        if (!string.IsNullOrWhiteSpace(apiKey))
            await _settingsService.SetAsync("module.ai.apiKey", apiKey.Trim());
        await _settingsService.SetAsync("module.ai.model", string.IsNullOrWhiteSpace(model) ? null : model.Trim());
        return RedirectWithMessage("modules", success: "AI Assistant settings saved.", section: "ai");
    }

    // Blank apiKey/secretKey means "keep the existing value" -- mirrors OnPostSaveAiSettingsAsync.
    public async Task<IActionResult> OnPostSaveEmailSettingsAsync(
        string? apiKey, string? secretKey, string? fromAddress, string? fromName)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        CurrentUserId = User.GetIdentity().userId;
        if (!await ComputeCanManageProtectedSettingsAsync()) return Forbid();

        if (!string.IsNullOrWhiteSpace(apiKey))
            await _settingsService.SetAsync(InventoryStore.App.Email.EmailSender.ApiKeySettingKey, apiKey.Trim());
        if (!string.IsNullOrWhiteSpace(secretKey))
            await _settingsService.SetAsync(InventoryStore.App.Email.EmailSender.SecretKeySettingKey, secretKey.Trim());
        await _settingsService.SetAsync(InventoryStore.App.Email.EmailSender.FromAddressSettingKey,
            string.IsNullOrWhiteSpace(fromAddress) ? null : fromAddress.Trim());
        await _settingsService.SetAsync(InventoryStore.App.Email.EmailSender.FromNameSettingKey,
            string.IsNullOrWhiteSpace(fromName) ? null : fromName.Trim());

        return RedirectWithMessage("modules", success: "Email settings saved.", section: "email");
    }

    public async Task<IActionResult> OnPostSendTestEmailAsync(string testAddress)
    {
        if (!User.IsInRole("Admin")) return Forbid();
        CurrentUserId = User.GetIdentity().userId;
        if (!await ComputeCanManageProtectedSettingsAsync()) return Forbid();

        if (string.IsNullOrWhiteSpace(testAddress))
            return RedirectWithMessage("modules", error: "Enter an address to send the test email to.", section: "email");

        var result = await _emailSender.SendAsync(testAddress.Trim(), "Inventory Store test email",
            "<p>This is a test email from your Inventory Store instance. If you received this, email delivery is working.</p>");

        return result.Succeeded
            ? RedirectWithMessage("modules", success: $"Test email sent to {testAddress.Trim()}.", section: "email")
            : RedirectWithMessage("modules", error: result.Error, section: "email");
    }

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

    private IActionResult RedirectWithMessage(string tab, string? success = null, string? error = null, string? section = null) =>
        RedirectToPage(new { tab, success, error, section });

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
