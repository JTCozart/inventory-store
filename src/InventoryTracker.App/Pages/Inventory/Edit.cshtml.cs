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
    public IEnumerable<string> Locations { get; private set; } = [];

    public EditModel(IInventoryService inventoryService, ICategoryService categoryService)
    {
        _inventoryService = inventoryService;
        _categoryService  = categoryService;
    }

    public IActionResult OnGetAsync(int id)
    {
        return RedirectToPage("/Inventory/Index", new { open = id });
    }

    public async Task<IActionResult> OnPostCreateCategoryAsync(string name, string? color)
    {
        try
        {
            var cat = await _categoryService.CreateAsync(new CreateCategoryDto(name, color));
            return new JsonResult(new { id = cat.Id, name = cat.Name, color = cat.Color });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
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
