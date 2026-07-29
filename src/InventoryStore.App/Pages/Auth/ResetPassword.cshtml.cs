using System.ComponentModel.DataAnnotations;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Auth;

public class ResetPasswordModel : PageModel
{
    private readonly IUserAuthService _authService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public bool Invalid { get; set; }

    public ResetPasswordModel(IUserAuthService authService)
    {
        _authService = authService;
    }

    public IActionResult OnGet(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            Invalid = true;
            return Page();
        }
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Input.NewPassword != Input.ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        var succeeded = await _authService.ResetPasswordWithTokenAsync(Input.Token, Input.NewPassword);
        if (!succeeded)
        {
            Invalid = true;
            return Page();
        }

        return RedirectToPage("/Auth/Login", new { success = "Password reset. You can now sign in." });
    }

    public class InputModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
