using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using InventoryTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IUserAuthService _authService;
    private readonly INtfyService _ntfy;

    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public LoginModel(IUserAuthService authService, INtfyService ntfy)
    {
        _authService = authService;
        _ntfy        = ntfy;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");
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
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        _ = _ntfy.NotifyLoginAsync(user.DisplayName);
        return new OkResult();
    }

    public class LoginInputModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
