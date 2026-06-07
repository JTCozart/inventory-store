using System.ComponentModel.DataAnnotations;
using InventoryTracker.App.Extensions;
using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using InventoryTracker.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages.Inventory;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IInventoryService _inventoryService;

    [BindProperty]
    public CreateInputModel Input { get; set; } = new();

    public CreateModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var (userId, username) = User.GetIdentity();
        await _inventoryService.CreateItemAsync(new CreateInventoryItemDto(
            Input.Name, Input.Quantity, Input.Description, Input.Location, Input.SKU,
            Input.MinimumQuantity, Input.ItemType, Input.ScanWarning
        ), userId, username);

        return RedirectToPage("./Index");
    }

    public class CreateInputModel
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        public string? Description { get; set; }
        public string? Location { get; set; }
        public string? SKU { get; set; }

        [Range(0, int.MaxValue)]
        public int MinimumQuantity { get; set; }

        public ItemType ItemType { get; set; } = ItemType.Consumable;

        [MaxLength(500)]
        public string? ScanWarning { get; set; }
    }
}
