using InventoryStore.Application.DTOs;
using InventoryStore.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryStore.App.Pages.Public;

public class IndexModel : PageModel
{
    private readonly IInventoryService _inventoryService;
    private readonly ISettingsService  _settingsService;

    public IEnumerable<InventoryItemDto> Items { get; private set; } = [];
    public bool IsEnabled { get; private set; }

    public IndexModel(IInventoryService inventoryService, ISettingsService settingsService)
    {
        _inventoryService = inventoryService;
        _settingsService  = settingsService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Allow embedding in iframes from any origin
        Response.Headers["X-Frame-Options"]         = "ALLOWALL";
        Response.Headers["Content-Security-Policy"] = "frame-ancestors *";

        IsEnabled = await _settingsService.GetAsync("public.view.enabled") == "true";
        if (IsEnabled)
            Items = await _inventoryService.GetPublicItemsAsync();

        return Page();
    }
}
