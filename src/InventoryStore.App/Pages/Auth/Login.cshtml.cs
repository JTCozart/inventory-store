using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IUserAuthService _authService;
    private readonly INtfyService _ntfy;

    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }

    public LoginModel(IUserAuthService authService, INtfyService ntfy)
    {
        _authService = authService;
        _ntfy        = ntfy;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(ResolveDestination(returnUrl, User.IsInRole(nameof(InventoryStore.Domain.Enums.UserRole.Staff))));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var username = Request.Form["username"].ToString().Trim();
        var password = Request.Form["password"].ToString();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return BadRequest("Username and password are required.");

        var user = await _authService.ValidateCredentialsAsync(username, password);
        if (user is null)
            return BadRequest("Invalid username or password.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(InventoryStore.App.Extensions.ClaimsPrincipalExtensions.UsernameClaimType, user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        _ = _ntfy.NotifyLoginAsync(user.DisplayName);

        // Respect where they came from (e.g. /Terminal); otherwise Staff land on the Terminal
        // and everyone else on the main app.
        var returnUrl = Request.Form["returnUrl"].ToString();
        var redirect  = ResolveDestination(returnUrl, user.Role == InventoryStore.Domain.Enums.UserRole.Staff);
        return new JsonResult(new { redirect });
    }

    // A valid local return URL wins; failing that, Staff go to the Terminal and others to the app root.
    private string ResolveDestination(string? returnUrl, bool isStaff)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return returnUrl;
        return isStaff ? "/Terminal" : "/";
    }

    public class LoginInputModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
