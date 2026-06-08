using System.ComponentModel.DataAnnotations;
using InventoryTracker.App.Extensions;
using InventoryTracker.Application.DTOs;
using InventoryTracker.Application.Interfaces.Services;
using InventoryTracker.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryTracker.App.Pages.Inventory;

[Authorize(Roles = "Admin,Manager")]
public class CreateModel : PageModel
{
    private readonly IInventoryService _inventoryService;
    private readonly ICategoryService _categoryService;

    [BindProperty]
    public CreateInputModel Input { get; set; } = new();

    public IEnumerable<CategoryDto> Categories { get; private set; } = [];

    public CreateModel(IInventoryService inventoryService, ICategoryService categoryService)
    {
        _inventoryService = inventoryService;
        _categoryService  = categoryService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Categories = await _categoryService.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Categories = await _categoryService.GetAllAsync();
            return Page();
        }

        var (userId, username) = User.GetIdentity();
        await _inventoryService.CreateItemAsync(new CreateInventoryItemDto(
            Input.Name, Input.Quantity, Input.Description, Input.Location, Input.SKU,
            Input.MinimumQuantity, Input.ItemType, Input.ScanWarning, Input.CategoryId, Input.ExpiryDate
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

        public int? CategoryId { get; set; }
        public DateOnly? ExpiryDate { get; set; }
    }
}
