using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages.UserGuide;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
