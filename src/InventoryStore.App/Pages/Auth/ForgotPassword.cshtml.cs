using System.ComponentModel.DataAnnotations;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Auth;

public class ForgotPasswordModel : PageModel
{
    private readonly IUserAuthService _authService;
    private readonly IEmailSender _emailSender;
    private readonly InventoryStore.App.Modules.IModuleRegistry _modules;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Submitted { get; set; }

    public ForgotPasswordModel(IUserAuthService authService, IEmailSender emailSender,
        InventoryStore.App.Modules.IModuleRegistry modules)
    {
        _authService = authService;
        _emailSender = emailSender;
        _modules     = modules;
    }

    private async Task<bool> IsAvailableAsync() =>
        await _modules.IsEnabledAsync("email") && await _emailSender.IsConfiguredAsync();

    public async Task<IActionResult> OnGetAsync()
    {
        // The link only makes sense once email is actually configured; otherwise send
        // people back to the login page's system-tray fallback hint.
        if (!await IsAvailableAsync())
            return RedirectToPage("/Auth/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await IsAvailableAsync())
            return RedirectToPage("/Auth/Login");

        if (!ModelState.IsValid)
            return Page();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        await _authService.RequestPasswordResetAsync(Input.UsernameOrEmail.Trim(), baseUrl);

        // Enumeration-safe: show the same confirmation regardless of whether the account
        // exists or has an email on file.
        Submitted = true;
        return Page();
    }

    public class InputModel
    {
        [Required]
        public string UsernameOrEmail { get; set; } = string.Empty;
    }
}
