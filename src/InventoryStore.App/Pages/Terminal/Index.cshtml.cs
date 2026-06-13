using InventoryStore.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Terminal;

// The Terminal is a stripped-down, touch-first screen for front-desk / shop-floor use.
// Staff accounts are limited to it; Admins and Managers can also reach it from the main nav
// and, unlike Staff, may restock from here.
[Authorize(Roles = "Admin,Manager,Staff")]
public class IndexModel : PageModel
{
    // Restock adds stock, so it stays an Admin/Manager ability even at the Terminal.
    public bool CanRestock { get; private set; }
    public string DisplayName { get; private set; } = "";
    public string RoleName { get; private set; } = "";

    public void OnGet()
    {
        CanRestock  = User.IsInRole(nameof(UserRole.Admin)) || User.IsInRole(nameof(UserRole.Manager));
        DisplayName = User.Identity?.Name ?? "";
        RoleName    = User.IsInRole(nameof(UserRole.Admin)) ? "Admin"
                    : User.IsInRole(nameof(UserRole.Manager)) ? "Manager"
                    : "Staff";
    }
}
