using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Auth;

public class SetupModel : PageModel
{
    private readonly IUserAuthService _authService;
    private readonly ISettingsService _settings;
    private readonly InventoryStore.App.Services.AppTimeZone _appTimeZone;

    [BindProperty]
    public SetupInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<InventoryStore.App.Utilities.TimeZones.TzOption> AvailableTimeZones
        => InventoryStore.App.Utilities.TimeZones.Options;

    public SetupModel(IUserAuthService authService, ISettingsService settings,
        InventoryStore.App.Services.AppTimeZone appTimeZone)
    {
        _authService = authService;
        _settings    = settings;
        _appTimeZone = appTimeZone;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _authService.IsSetupRequiredAsync())
            return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Input.Password != Input.ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        try
        {
            var user = await _authService.SetupAdminAsync(
                Input.Username.Trim(), Input.Email, Input.Password, Input.FirstName, Input.LastName);

            // Persist the account's time zone chosen during setup (empty = use each viewer's device).
            var tz = Input.TimeZoneId?.Trim();
            if (!string.IsNullOrEmpty(tz) && InventoryStore.App.Utilities.TimeZones.IsValid(tz))
            {
                await _settings.SetAsync(InventoryStore.App.Services.AppTimeZone.SettingKey, tz);
                _appTimeZone.Set(tz);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.DisplayName),
                new(InventoryStore.App.Extensions.ClaimsPrincipalExtensions.UsernameClaimType, user.Username),
                new(ClaimTypes.Role, user.Role.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    public class SetupInputModel
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        [Required, MinLength(3), MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string ConfirmPassword { get; set; } = string.Empty;

        // IANA time zone id for the account's location (empty = use each viewer's device zone).
        public string? TimeZoneId { get; set; }
    }
}
