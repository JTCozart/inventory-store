using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Auth;

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // Carry the page they logged out from (e.g. /Terminal) through to login so they
        // come back to the same place after signing in again.
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return RedirectToPage("/Auth/Login", new { returnUrl });
        return RedirectToPage("/Auth/Login");
    }
}
