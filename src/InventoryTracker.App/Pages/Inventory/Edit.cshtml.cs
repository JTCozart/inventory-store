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
public class EditModel : PageModel
{
    private readonly IInventoryService _inventoryService;
    private readonly ICategoryService _categoryService;

    [BindProperty]
    public EditInputModel Input { get; set; } = new();

    [BindProperty]
    public int ItemId { get; set; }

    public ItemType DisplayItemType { get; private set; }
    public IEnumerable<CategoryDto> Categories { get; private set; } = [];

    public EditModel(IInventoryService inventoryService, ICategoryService categoryService)
    {
        _inventoryService = inventoryService;
        _categoryService  = categoryService;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var item = await _inventoryService.GetItemAsync(id);
        if (item is null) return NotFound();

        Categories      = await _categoryService.GetAllAsync();
        ItemId          = id;
        DisplayItemType = item.ItemType;
        Input = new EditInputModel
        {
            Name            = item.Name,
            Quantity        = item.Quantity,
            Description     = item.Description,
            Location        = item.Location,
            SKU             = item.SKU,
            MinimumQuantity = item.MinimumQuantity,
            ScanWarning     = item.ScanWarning,
            CategoryId      = item.CategoryId,
            ExpiryDate      = item.ExpiryDate
        };

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
        await _inventoryService.UpdateItemAsync(ItemId, new UpdateInventoryItemDto(
            Input.Name, Input.Quantity, Input.Description, Input.Location,
            Input.SKU, Input.MinimumQuantity, Input.ScanWarning, Input.CategoryId, Input.ExpiryDate
        ), userId, username);

        return RedirectToPage("./Index");
    }

    public class EditInputModel
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

        [MaxLength(500)]
        public string? ScanWarning { get; set; }

        public int? CategoryId { get; set; }
        public DateOnly? ExpiryDate { get; set; }
    }
}
